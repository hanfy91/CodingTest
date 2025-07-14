using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Net
{
    /// <summary>
    ///  网络管理器
    ///  负责连接、发送、接收数据等功能
    ///  支持缓存服务和消息编码解码
    ///  使用单例模式，确保全局唯一
    ///  TODO 可以把真正网络层再封装一层，用于支持不同形态，TCP、UDP、WebSocket等
    ///  TODO 粘包处理 重连机制  心跳机制
    /// </summary>
    public class NetManager:MonoBehaviour
    {
        public static NetManager Instance { get; private set; }
        private int m_MaxSendCount = 10;
        private int m_RecvTimeOut = 1000; // Receive timeout in milliseconds
        private int m_BufferSize = 1024; // Buffer size for receiving data
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        private void Update()
        {
            if (!m_IsInitialized)
            {
                return;
            }
            int max = 20;
            while (max-- > 0 && m_RecvQueue.TryDequeue(out var data))
            {
                Receive(data);
            }
        }
        private void OnDestroy()
        {
            Disconnect();
            m_CacheService?.ClearCache();
            m_CacheService = null;
            m_Encoder = null;
            m_Decoder = null;
            m_HandlerMap.Clear();
            m_Tcp = null;
            m_RecvThread = null;
            m_SendThread = null;
            Instance = null;
        }
        private ICacheService m_CacheService;
        private IMessageEncoder m_Encoder;
        private IMessageDecoder m_Decoder;
        private Dictionary<int,IMessageHandler> m_HandlerMap = new(16);
        /// <summary>
        /// TODO 可以把真正网络层再封装一层，用于支持不同形态，TCP、UDP、WebSocket等
        /// </summary>
        private TcpClient m_Tcp;
        private Thread m_RecvThread, m_SendThread;
        private NetworkStream m_Stream;
        private readonly object m_StreamLock = new();
        private ConcurrentQueue<byte[]> m_RecvQueue = new();
        private ConcurrentQueue<byte[]> m_SendQueue = new();
        private bool m_IsInitialized = false;
        private bool m_IsConneted = false;
        public bool IsConnected => m_IsConneted;
        /// 连接成功和断开连接的回调
        public Action OnConnect;
        public Action OnDisConnect;
        /// <summary>
        /// 初始化网络管理器
        /// </summary>
        /// <param name="cacheService">缓存服务 </param>
        /// <param name="encoder">解码器 用于支持不同种类网络协议  pb.messagepack</param>
        /// <param name="decoder">编码器 </param>
        public void Init(ICacheService cacheService,IMessageEncoder encoder, IMessageDecoder decoder)
        {
            // Initialize the network manager with cache service, encoder, and decoder
            Debug.Log("NetManager initialized with cache service, encoder, and decoder.");
            m_IsInitialized = true;
            m_CacheService = cacheService;
            m_CacheService.RegisterSendFunction(SendMessage);
            m_Encoder = encoder;
            m_Decoder = decoder;
        }
        public void Connect(string address, int port)
        {
            // Implement connection logic here
            Debug.Log($"Connecting to {address}:{port}");
            m_Tcp = new TcpClient(address, port);
            m_Stream = m_Tcp.GetStream();
            m_IsConneted = true;

            m_RecvThread = new Thread(RecvLoop);
            m_SendThread = new Thread(SendLoop);
            m_RecvThread.Start();
            m_SendThread.Start();
        }
        public void Disconnect()
        {
            // Implement disconnection logic here
            Debug.Log("Disconnecting");
            m_IsConneted = false;
            m_RecvThread?.Join();
            m_SendThread?.Join();
            m_Tcp?.Close();
            m_RecvQueue.Clear();
            m_SendQueue.Clear();
            m_HandlerMap.Clear();
        }
        // 请求是否是从CacheService 发过来的,常规上层业务需要先写到缓存里
        public void SendOutMessage(IMessage message,IMessageHandler callback, bool isFromCache = false)
        {
            if (!m_IsInitialized)
            {
                return;
            }
            if (m_HandlerMap.ContainsKey(message.ReqId))
            {
                Debug.LogError("Message with this ReqId already exists in handler map, skipping send.");
                return;
            }
            if (callback != null)
            {
                m_HandlerMap[message.ReqId] = callback;
                Debug.Log($"Handler registered for message with ReqId: {message.ReqId}");
            }
            else
            {
                Debug.LogWarning("No handler provided for message, it will not be processed.");
            }
            // Implement message sending logic here
            if (!isFromCache)
            {
                m_CacheService.CacheMessage(message.ReqId, m_Encoder.Encode(message));
                Debug.Log($"Message cached for later sending: {message}");
            }
            else
            {
                SendMessage(m_Encoder.Encode(message));
                // Send the message immediately
                Debug.Log($"Sending message: {message}");
            }
        }
        
        public void SendMessage(byte[] bytes)
        {
            m_SendQueue.Enqueue(bytes);
        }
        private void SendLoop()
        {
            while (m_IsConneted)
            {
                int sendCount = 0;
                while (m_SendQueue.TryDequeue(out var data))
                {
                    if (data != null)
                    {
                        lock (m_StreamLock)
                        {
                            if (!m_IsConneted || m_Tcp == null || !m_Tcp.Connected)
                            {
                                Debug.LogError("TCP connection is not established, cannot send data.");
                                return;
                            }
                            m_Stream.Write(data, 0, data.Length);
                            sendCount++;
                        }
                    }
                    if (sendCount >= m_MaxSendCount)
                    {
                        Thread.Sleep(100);
                        sendCount = 0;
                    }
                }
            }
        }
        private void RecvLoop()
        {
            try
            {
                while (m_IsConneted)
                {
                    byte[] buffer = new byte[m_BufferSize];
                    int len = m_Stream.Read(buffer, 0, buffer.Length);
                    if (len > 0)
                    {
                        byte[] data = new byte[len];
                        Array.Copy(buffer, 0, data, 0, len);
                        m_RecvQueue.Enqueue(data);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RecvLoop] Exception: {ex.Message}");
                m_IsConneted = false;
            }
        }
        public void Receive(byte[] data)
        {
            IMessage message = m_Decoder.Decode(data);
            if (message == null)
            {
                Debug.LogError("Received null message, skipping.");
                return;
            }
            m_CacheService.RemoveCachedMessage(message.ReqId);
            if (m_HandlerMap.TryGetValue(message.ReqId,out var handler))
            {
                handler.Handle(message);
                m_HandlerMap.Remove(message.ReqId); // 防泄漏
            }
            // Implement message receiving logic here
            Debug.Log($"Received message: {message}");
        }
       
    }
    /// <summary>
    /// 本地缓存服务接口
    /// </summary>
    public interface ICacheService
    {
        void RegisterSendFunction(Action<byte[]> sendFunction);
        void CacheMessage(int reqId, byte[] message);
        void RemoveCachedMessage(int reqId);
        /// <summary>
        /// 发送缓存消息驱动
        /// </summary>
        void Tick();
        void ClearCache();
    }
}
