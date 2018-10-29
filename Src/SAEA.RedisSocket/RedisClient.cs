﻿/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket
*文件名： RedisOperation
*版本号： V3.1.1.0
*唯一标识：23cf910b-3bed-4d80-9e89-92c04fba1e5e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/16 10:12:40
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/16 10:12:40
*修改人： yswenli
*版本号： V3.1.1.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Core;
using SAEA.RedisSocket.Interface;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SAEA.RedisSocket
{
    /// <summary>
    /// redis连接类
    /// 支持常用命令
    /// 支持Cluster
    /// </summary>
    public partial class RedisClient : IClient
    {
        RedisConnection _cnn;

        RedisDataBase _redisDataBase;

        const string OK = RedisConst.OK;

        object _syncLocker = new object();

        int _dbIndex = 0;

        bool _debugModel = false;

        public bool IsConnected { get; set; }

        public RedisConfig RedisConfig
        {
            get;
            set;
        }

        public RedisClient(RedisConfig config, bool debugModel = false)
        {
            _debugModel = debugModel;
            RedisConfig = config;
            _cnn = new RedisConnection(RedisConfig.GetIPPort(), RedisConfig.ActionTimeOut, debugModel);
            _cnn.OnDisconnected += _cnn_OnDisconnected;
        }

        public RedisClient(string connectStr, bool debugModel = false) : this(new RedisConfig(connectStr), debugModel) { }

        public RedisClient(string ipPort, string password, int acitonTimeout = 60, bool debugModel = false) : this(new RedisConfig(ipPort, password, acitonTimeout), debugModel)
        {

        }

        /// <summary>
        /// 连接断开事件
        /// </summary>
        /// <param name="obj"></param>
        private void _cnn_OnDisconnected(string ipPort)
        {
            RedisConnectionManager.Remove(ipPort);
            RedisConnectionManager.RemoveClusterNode(ipPort);
        }

        /// <summary>
        /// 使用密码连接到RedisServer
        /// </summary>
        /// <returns></returns>
        public string Connect()
        {
            lock (_syncLocker)
            {
                if (!IsConnected)
                {
                    _cnn.Connect();

                    IsConnected = _cnn.IsConnected;

                    var infoMsg = Info();

                    if (infoMsg.Contains(RedisConst.NOAuth))
                    {
                        if (string.IsNullOrEmpty(RedisConfig.Passwords))
                        {
                            _cnn.Quit();
                            return infoMsg;
                        }

                        var authMsg = Auth(RedisConfig.Passwords);

                        if (string.Compare(authMsg, OK, true) != 0)
                        {
                            _cnn.Quit();
                            return authMsg;
                        }
                    }
                }
                _cnn.KeepAlived(() => this.KeepAlive());
                var ipPort = RedisConfig.GetIPPort();

                var isMaster = this.IsMaster;
                var isCluster = this.IsCluster;

                if (isCluster)
                {
                    _cnn.RedisServerType = isMaster ? RedisServerType.ClusterMaster : RedisServerType.ClusterSlave;
                    GetClusterMap(ipPort);
                }
                else
                {
                    _cnn.RedisServerType = isMaster ? RedisServerType.Master : RedisServerType.Slave;
                    RedisConnectionManager.Set(ipPort, _cnn);
                }
                return OK;
            }
        }


        /// <summary>
        /// 保持redis连接
        /// </summary>
        private void KeepAlive()
        {
            ThreadHelper.Run(() =>
            {
                while (_cnn.IsConnected)
                {
                    if (_cnn.Actived <= DateTimeHelper.Now.AddSeconds(60))
                    {
                        Ping();
                    }
                    ThreadHelper.Sleep(30 * 1000);
                }
            }, true, ThreadPriority.Highest);
        }

        /// <summary>
        /// redis 密码验证
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public string Auth(string password)
        {
            return GetDataBase().DoInOne(RequestType.AUTH, password).Data;
        }

        /// <summary>
        /// redis ping
        /// </summary>
        /// <returns></returns>
        public string Ping()
        {
            return GetDataBase().Do(RequestType.PING).Data;
        }

        /// <summary>
        /// 选择redis database
        /// </summary>
        /// <param name="dbIndex"></param>
        /// <returns></returns>
        public bool Select(int dbIndex = -1)
        {
            if (dbIndex > -1)
            {
                _dbIndex = dbIndex;
            }
            if (GetDataBase().DoInOne(RequestType.SELECT, _dbIndex.ToString()).Data.IndexOf(RedisConst.ErrIndex) == -1)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 当前db数据项数据
        /// </summary>
        /// <returns></returns>
        public int DBSize()
        {
            var result = 0;
            int.TryParse(GetDataBase().Do(RequestType.DBSIZE).Data, out result);
            return result;
        }
        /// <summary>
        /// 获取某个集合的类型 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Type(string key)
        {
            return GetDataBase().Do1(RequestType.TYPE, key, true).Data;
        }
        /// <summary>
        /// redis server的信息
        /// </summary>
        /// <returns></returns>
        public string Info(string section = RedisConst.All)
        {
            return GetDataBase().DoInOne(RequestType.INFO, section).Data;
        }

        /// <summary>
        /// redis信息
        /// </summary>
        public ServerInfo ServerInfo
        {
            get
            {
                var info = Info();

                var lines = info.Split(RedisConst.Enter, StringSplitOptions.RemoveEmptyEntries);

                Dictionary<string, string> dic = new Dictionary<string, string>();

                foreach (var item in lines)
                {
                    if (item.IndexOf("#") > -1)
                        continue;
                    var arr = item.Split(":");
                    dic.Add(arr[0], arr[1]);
                }

                var serverInfo = new ServerInfo()
                {
                    config_file = dic["config_file"],
                    connected_clients = dic["connected_clients"],
                    connected_slaves = dic["connected_slaves"],
                    os = dic["os"],
                    redis_version = dic["redis_version"],
                    role = dic["role"],
                    used_cpu_sys = dic["used_cpu_sys"],
                    used_cpu_user = dic["used_cpu_user"],
                    used_memory = dic["used_memory"],
                    used_memory_human = dic["used_memory_human"],
                    used_memory_peak_human = dic["used_memory_peak_human"],
                    address = RedisConfig.GetIPPort()
                };

                if (dic.ContainsKey("cluster_enabled"))
                {
                    serverInfo.cluster_enabled = dic["cluster_enabled"];
                }
                if (dic.ContainsKey("executable"))
                {
                    serverInfo.executable = dic["executable"];
                }
                if (dic.ContainsKey("maxmemory_human"))
                {
                    serverInfo.maxmemory_human = dic["maxmemory_human"];
                }
                if (dic.ContainsKey("used_memory_rss_human"))
                {
                    serverInfo.used_memory_rss_human = dic["used_memory_rss_human"];
                }
                return serverInfo;
            }
        }
        /// <summary>
        /// 设置或取消丛
        /// </summary>
        /// <param name="ipPort">格式：ip port，若为空则为取消</param>
        /// <returns></returns>
        public string SlaveOf(string ipPort = RedisConst.Empty)
        {
            return GetDataBase().DoInOne(RequestType.SLAVEOF, string.IsNullOrEmpty(ipPort) ? RedisConst.NoOne : ipPort).Data;
        }
        /// <summary>
        /// redis server是否是主
        /// </summary>
        /// <returns></returns>
        public bool IsMaster
        {
            get
            {
                var info = Info("Replication");
                if (info.Contains("role:master"))
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 当前db index
        /// </summary>
        public int DBIndex
        {
            get
            {
                return _dbIndex;
            }
        }

        /// <summary>
        /// 获取redis database操作
        /// </summary>
        /// <param name="dbIndex"></param>
        /// <returns></returns>
        public RedisDataBase GetDataBase(int dbIndex = -1)
        {
            lock (_syncLocker)
            {
                if (dbIndex >= 0 && dbIndex != _dbIndex)
                {
                    _dbIndex = dbIndex;
                    Select(_dbIndex);
                }
                if (_redisDataBase == null)
                {
                    _redisDataBase = new RedisDataBase(_cnn);

                    _redisDataBase.OnRedirect += _redisDataBase_OnRedirect;
                }
                return _redisDataBase;
            }
        }

    }
}
