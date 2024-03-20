﻿using Assets.Scripts.Common.DataStructure;
using Assets.Scripts.Common.Network.Packet;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace Assets.Scripts.Common.Network
{
    public class Client
    {
        public int _id = 0;

        TcpClient _client = new TcpClient();

        RingBuffer _temp_buffer = new RingBuffer();
        RingBuffer _read_buffer = new RingBuffer();

        public void Send(BasePacket packet)
        {
            try
            {
                byte[] buffer = new byte[GameManager.SEND_BUFFER_SIZE];
                int buffer_length = packet.Serialize(ref buffer);

                _client.GetStream().BeginWrite(buffer, 0, buffer_length, OnSend, packet);
            }
            catch (Exception ex)
            {
                if (ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    Debug.LogError($"client disconnected(Send()). id : {_id}, msg : {ex.Message}");
                }
            }
        }

        public void Receive()
        {
            try
            {
                _read_buffer.Clear();
                _client.GetStream().BeginRead(_read_buffer.Data(), 0, _read_buffer.GetLength() - 1, OnReceive, null);
            }
            catch (Exception ex)
            {
                if (ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    Debug.LogError($"client disconnected(Receive()). id : {_id}, msg : {ex.Message}");
                }
            }
        }

        public void Connect(string ip, int port)
        {
            _client.BeginConnect(ip, port, OnConnect, null);
        }

        public void Close()
        {
            _client.Close();
        }

        private void OnConnect(IAsyncResult ar)
        {
            _client.EndConnect(ar);

            Receive();
        }

        private void OnSend(IAsyncResult ar)
        {
            try
            {
                _client.GetStream().EndWrite(ar);
            }
            catch (Exception ex)
            {
                if (ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    Debug.LogError($"client disconnected(OnSend()). id : {_id}, msg : {ex.Message}");
                }
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                int read_bytes = _client.GetStream().EndRead(ar);

                if (read_bytes == 0) // closed by server
                {
                    Debug.LogError($"server closed. id : {_id}");
                    _client.Close();
                    return;
                }

                if (false == _read_buffer.SetWriteIndex(read_bytes))
                {
                    Debug.LogError($"set write index error. read_bytes : {read_bytes}");
                    return;
                }

                byte[] packet_buffer = new byte[GameManager.READ_BUFFER_SIZE];
                int packet_size = 0;
                int remain_bytes = read_bytes;

                List<BasePacket> list_packet = new List<BasePacket>();

                while (remain_bytes > 0)
                {
                    if (false == CanMakePacket(ref packet_buffer, ref packet_size))
                    {
                        _temp_buffer.Clear();
                        _temp_buffer.Copy(ref _read_buffer);
                        break;
                    }

                    BasePacket? packet = MakePacket(ref packet_buffer);

                    if (null != packet)
                    {
                        if (packet._packet_id == PacketID.sc_login)
                        {
                            _id = ((sc_login)packet)._my_info._client_id;
                        }

                        PacketContext context = new PacketContext(this, packet);

                        GameManager.ZONE?.PushPacketContext(context);
                    }
                    else
                    {
                        Debug.LogError($"MakePacket() returned nullptr.");
                    }

                    Array.Clear(packet_buffer, 0, packet_buffer.Length);

                    remain_bytes -= packet_size;
                }

                Receive();
            }
            catch (Exception ex)
            {
                if (ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    Debug.LogError($"client disconnected(OnReceive()). id : {_id}, msg : {ex.Message}");
                }
            }
        }

        private bool CanMakePacket(ref byte[] packet_buffer, ref int packet_size)
        {
            int current_size = 0;

            if (false == _temp_buffer.Empty())
            {
                current_size += _temp_buffer.PopAll(new ArraySegment<byte>(packet_buffer, current_size, packet_buffer.Length));
            }
            else
            {
                if (_read_buffer.Empty())
                {
                    Debug.LogError($"read buffer empty.");
                    return false;
                }

                if (false == _read_buffer.Pop(new ArraySegment<byte>(packet_buffer, 0, packet_buffer.Length), sizeof(int)))
                {
                    Debug.LogError($"get sizeof(int) failed in read buffer.");
                    return false;
                }

                current_size = sizeof(int);
            }

            packet_size = BitConverter.ToInt32(packet_buffer, 0);

            if (false == _read_buffer.Pop(new ArraySegment<byte>(packet_buffer, current_size, packet_buffer.Length - current_size), packet_size - current_size))
            {
                Debug.LogError($"get (packet_size({packet_size}) - current_size({current_size})) failed in read buffer.");
                return false;
            }

            return true;
        }

        private BasePacket? MakePacket(ref byte[] packet_buffer)
        {
            PacketID packet_id = (PacketID)BitConverter.ToInt32(packet_buffer, sizeof(int));

            BasePacket? packet = null;

            switch (packet_id)
            {
                case PacketID.sc_login:
                    packet = new sc_login();
                    packet.Deserialize(ref packet_buffer);
                    break;
                case PacketID.sc_welcome:
                    packet = new sc_welcome();
                    packet.Deserialize(ref packet_buffer);
                    break;
                case PacketID.sc_move:
                    packet = new sc_move();
                    packet.Deserialize(ref packet_buffer);
                    break;
                case PacketID.sc_logout:
                    packet = new sc_logout();
                    packet.Deserialize(ref packet_buffer);
                    break;
                default:
                    break;
            }

            return packet;
        }
    }
}
