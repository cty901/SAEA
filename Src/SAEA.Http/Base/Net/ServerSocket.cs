﻿/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Http.Base.Net
*文件名： ServerSocket
*版本号： V3.1.1.0
*唯一标识：ab912b9a-c7ed-44d9-8e48-eef0b6ff86a2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 17:11:15
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 17:11:15
*修改人： yswenli
*版本号： V3.1.1.0
*描述：
*
*****************************************************************************/
using SAEA.Http.Model;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.Http.Base.Net
{
    class ServerSocket : BaseServerSocket
    {
        public event Action<IUserToken, IRequestDataReader> OnRequested;


        public ServerSocket(int bufferSize = 1024 * 10, int count = 10000) : base(new HContext(), bufferSize, count)
        {
            
        }

        protected override void OnReceiveBytes(IUserToken userToken, byte[] data)
        {
            HUnpacker coder = (HUnpacker)userToken.Unpacker;

            coder.IsAnalysis = false;

            coder.GetRequest(data, (result) =>
            {
                OnRequested?.Invoke(userToken, result);
            });
        }

        /// <summary>
        /// 发送消息并结束会话
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        public void End(IUserToken userToken, byte[] data)
        {
            base.End(userToken, data);
        }
    }
}
