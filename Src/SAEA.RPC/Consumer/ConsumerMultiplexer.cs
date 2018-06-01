﻿/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Consumer
*文件名： ConsumerMultiplexer
*版本号： V1.0.0.0
*唯一标识：85b40df2-6436-4a63-8358-6a0ed32b20cd
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/25 16:14:32
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/25 16:14:32
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Commom;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Concurrent;

namespace SAEA.RPC.Consumer
{
    /// <summary>
    /// 使用多路复用概念来实现高效率传输
    /// </summary>
    public class ConsumerMultiplexer : ISyncBase, IDisposable
    {
        static HashMap<string, int, RClient> _hashMap = new HashMap<string, int, RClient>();

        int _index = 0;

        Uri _uri = null;

        int _links = 4;

        ConcurrentDictionary<int, RClient> _myClients = new ConcurrentDictionary<int, RClient>();


        object _syncLocker=new object();
        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="links"></param>
        ConsumerMultiplexer(Uri uri, int links = 10)
        {
            _uri = uri;
            _links = links;
            var dic = _hashMap.GetAll(uri.ToString());

            for (int i = 0; i < _links; i++)
            {
                var rClient = dic[i];
                rClient.OnDisconnected += RClient_OnDisconnected;
                rClient.OnError += RClient_OnError;
                _myClients.TryAdd(i, rClient);
            }
        }

        /// <summary>
        /// 创建多路复用
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="links"></param>
        public static ConsumerMultiplexer Create(Uri uri, int links = 4)
        {
            if (!_hashMap.Exits(uri.ToString()))
            {
                for (int i = 0; i < links; i++)
                {
                    var rClient = new RClient(uri);
                    if (!rClient.Connect())
                    {
                        throw new RPCSocketException($"连接到{uri.ToString()}失败");
                    }
                    rClient.KeepAlive();
                    _hashMap.Set(uri.ToString(), i, rClient);
                }
            }
            return new ConsumerMultiplexer(uri, links);
        }


        #region events

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        private void RClient_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(ID, ex);
        }

        private void RClient_OnDisconnected(string ID, Exception ex)
        {
            foreach (var rClient in _myClients.Values)
            {
                if (rClient.UserToken.ID == ID)
                {
                    rClient.IsConnected = false;
                }
            }
            OnDisconnected?.Invoke(ID, ex);
        }


        #endregion

        /// <summary>
        /// 使用多路连接发送数据
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public byte[] Request(string serviceName, string method, byte[] args, int retry = 5)
        {
            return ReTryHelper.Do(() => this.GetClient().Request(serviceName, method, args), retry);
        }

        /// <summary>
        /// 获取缓存的连接
        /// </summary>
        /// <returns></returns>
        private RClient GetClient()
        {
            lock (_syncLocker)
            {
                RClient rClient;
                do
                {
                    if (_index >= _links) throw new RPCSocketException("连接已断开！");

                    if (_myClients.TryGetValue(_index, out rClient) && rClient.IsConnected)
                    {
                        break;
                    }
                    _index++;
                }
                while (!rClient.IsConnected);

                _index = 0;

                return rClient;
            }
        }

        public void Dispose()
        {
            foreach (var rClient in _myClients.Values)
            {
                rClient.Dispose();
            }
            _hashMap.Remove(_uri.ToString());
            _myClients.Clear();
        }
    }
}