using Assets.Scripts.Common.Network.Packet;
using Assets.Scripts.Game.Object;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Random = System.Random;
using Color = UnityEngine.Color;

namespace Assets.Scripts.Game.Zone
{
    public class Zone
    {
        struct Pos
        {
            public int _x;
            public int _y;

            public Pos(int x, int y)
            {
                _x = x;
                _y = y;
            }
        }

        private ConcurrentQueue<PacketContext> _packet_context_queue = new ConcurrentQueue<PacketContext>();

        private List<List<Tile>> _tile = new List<List<Tile>>();
        private Dictionary<int, Pos> _object_info = new Dictionary<int, Pos>(); // key : client id

        private Random _random = new Random();
        
        private bool _running = true;

        private GameObject floor = null;

        public Zone(string map_file_name)
        {
            // set map data
            var map_str = Resources.Load<TextAsset>("map");

            string[] arr_newline_str = map_str.text.Split(System.Environment.NewLine);

            int count_x = 0;
            int count_y = arr_newline_str.Length - 1;

            for (int i = 0; i < count_y; i++)
            {
                List<Tile> list_tile = new List<Tile>();

                string[] list_tile_data = arr_newline_str[i].Split(",");

                count_x = list_tile_data.Length;

                foreach (string tile_data in list_tile_data)
                {
                    Tile tile = new Tile();

                    if (tile_data == "-1")
                    {
                        tile._type = TileType.road;
                    }
                    else
                    {
                        tile._type = TileType.wall;
                    }

                    list_tile.Add(tile);
                }

                _tile.Add(list_tile);
            }

            // draw map data
            int floor_thickness = 1;
            int wall_height = 2;

            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(count_x, floor_thickness, count_y);
            floor.transform.position = new Vector3(0.0f, -(floor_thickness / 2.0f), 0.0f);
            floor.GetComponent<Renderer>().material.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            for (int y = 0; y < count_y; y++)
            {
                for (int x = 0; x < count_x; x++)
                {
                    if (_tile[y][x]._type == TileType.road)
                    {
                        continue;
                    }

                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.localScale = new Vector3(1.0f, wall_height, 1.0f);
                    wall.transform.position = new Vector3((x - count_x / 2) + 0.5f, wall_height / 2.0f, -(y - (count_y / 2)) - 1 + 0.5f);
                    wall.GetComponent<Renderer>().material.color = Color.grey;
                    wall.transform.SetParent(floor.transform);
                }
            }
        }

        public Task Start()
        {
            return Task.Run(() =>
            {
                while (_running)
                {
                    if (_packet_context_queue.TryDequeue(out PacketContext? context))
                    {
                        HandlePacketContext(context);
                    }

                    FakeInput? input = FakeInputContainer.GetInput();

                    if (input == null)
                    {
                        continue;
                    }

                    long now = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();

                    if (input._time_to_execute <= now)
                    {
                        bool finished = HandleFakeInput(input);

                        if (finished)
                        {
                            FakeInputContainer.RemoveInput();
                        }
                        else
                        {
                            input._time_to_execute += 5000; // 5초후 다시 시도
                        }
                    }
                }
            });
        }

        public void Stop()
        {
            _running = false;
        }

        public void PushPacketContext(PacketContext context)
        {
            _packet_context_queue.Enqueue(context);
        }

        public int GetCountX(int y)
        {
            return _tile[y].Count;
        }

        public int GetCountY()
        {
            return _tile.Count;
        }

        private bool CheckTile(int x, int y)
        {
            if (y < 0 || y >= _tile.Count)
            {
                return false;
            }

            if (x < 0 || x >= _tile[y].Count)
            {
                return false;
            }

            return _tile[y][x].IsEmpty();
        }

        private void SetObject(int x, int y, IObject obj)
        {
            _tile[y][x]._object = obj;
            _object_info.Add(obj._id, new Pos(x, y));
        }

        private void SetObject(int current_x, int current_y, int next_x, int next_y)
        {
            _tile[next_y][next_x]._object = _tile[current_y][current_x]._object;
            _tile[current_y][current_x]._object = null;

            _object_info.Remove(_tile[next_y][next_x]._object._id);
            _object_info.Add(_tile[next_y][next_x]._object._id, new Pos(next_x, next_y));
        }

        private void RemoveObject(int x, int y, int object_id)
        {
            _tile[y][x]._object = null;
            _object_info.Remove(object_id);
        }

        private void HandlePacketContext(PacketContext context)
        {
            switch (context._packet._packet_id)
            {
                case PacketID.sc_login:

                    ProcessPacket((sc_login)context._packet);

                    if (false == FakeInputContainer.Exist())
                    {
                        long time_to_execute = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() + _random.Next(1000, 3001); // 1 ~ 3초 추가
                        FakeInput input = new FakeInput(FakeInputType.move, time_to_execute); // move할지 attack(아직 미구현)할지 random으로 나중에 돌리기

                        Debug.Log($"fake input => type : {input._type}, time : {input._time_to_execute}");

                        FakeInputContainer.PushInput(input);
                    }

                    break;
                case PacketID.sc_welcome:
                    ProcessPacket((sc_welcome)context._packet);
                    break;
                case PacketID.sc_move:

                    int move_client_id = ((sc_move)context._packet)._move_client_id;
                    bool my_packet = (move_client_id == context._client._id);

                    ProcessPacket((sc_move)context._packet, move_client_id); // dummy client에서는, 자기가 받았을때만 처리. unity client에서는 모두 처리

                    if (my_packet)
                    {
                        if (false == FakeInputContainer.Exist())
                        {
                            long time_to_execute = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() + _random.Next(1000, 3001); // 1 ~ 3초 추가
                            FakeInput input = new FakeInput(FakeInputType.move, time_to_execute); // move할지 attack(아직 미구현)할지 random으로 나중에 돌리기

                            Debug.Log($"fake input => type : {input._type}, time : {input._time_to_execute}");

                            FakeInputContainer.PushInput(input);
                        }
                    }

                    break;
                case PacketID.sc_logout:

                    int logout_client_id = ((sc_logout)context._packet)._client_id;
                    ProcessPacket((sc_logout)context._packet, logout_client_id);

                    break;
                default:
                    break;
            }
        }

        private void ProcessPacket(sc_login packet)
        {
            SetAndDrawPlayer(packet._my_info._client_id, packet._my_info._x, packet._my_info._y, Color.yellow);

            foreach (sc_login.ClientPos pos in packet._list_client)
            {
                SetAndDrawPlayer(pos._client_id, pos._x, pos._y, Color.green);
            }
        }

        private void ProcessPacket(sc_welcome packet)
        {
            SetAndDrawPlayer(packet._client_id, packet._x, packet._y, Color.green);
        }

        private void ProcessPacket(sc_move packet, int client_id)
        {
            int current_x = 0;
            int current_y = 0;

            if (false == GetCurrentPos(out current_x, out current_y, packet._move_client_id))
            {
                Debug.Log($"sc_move => not found current pos. id : {packet._move_client_id}");
                return;
            }

            if (current_x == packet._x && current_y == packet._y)
            {
                Debug.Log($"sc_move => move failed. id : {packet._move_client_id}, x : {packet._x}, y : {packet._y}");
                return; // 이동 실패
            }

            if (CheckTile(packet._x, packet._y))
            {
                SetObject(current_x, current_y, packet._x, packet._y);

                DrawRequest drawRequest = new DrawRequest();
                drawRequest.SetDataForMove(packet._x, packet._y, client_id);

                GameManager.PushDrawRequest(drawRequest);

                Debug.Log($"sc_move => moved. id : {packet._move_client_id}, x : {packet._x}, y : {packet._y}");
            }
            else
            {
                Debug.Log($"sc_move => move failed (client). id : {packet._move_client_id}, x : {packet._x}, y : {packet._y}");
                return; // 서버는 승인했는데, client에서 이동 실패
            }
        }

        private void ProcessPacket(sc_logout packet, int client_id)
        {
            int current_x = 0;
            int current_y = 0;

            if (false == GetCurrentPos(out current_x, out current_y, client_id))
            {
                Debug.Log($"sc_logout => not found current pos. id : {client_id}");
                return;
            }

            RemoveObject(current_x, current_y, client_id);

            DrawRequest drawRequest = new DrawRequest();
            drawRequest.SetDataForRemove(client_id);

            GameManager.PushDrawRequest(drawRequest);

            Debug.Log($"sc_logout => id : {client_id}");
        }

        private bool HandleFakeInput(FakeInput input)
        {
            bool result = true;

            switch (input._type)
            {
                case FakeInputType.move:
                    result = ProcessFakeInputMove(0);
                    break;
                case FakeInputType.attack:
                    break;
                case FakeInputType.disconnect:
                    break;
                default:
                    break;
            }

            return result;
        }

        private bool ProcessFakeInputMove(int checked_count)
        {
            if (10 == checked_count) // 주위에 player들에게 둘러쌓이면, call stack overflow나서 check count 추가.
            {
                return false;
            }

            Pos pos;
            if (_object_info.TryGetValue(GameManager.CLIENT._id, out pos))
            {
                int next_pos_delta_x = _random.Next(-1, 2); // -1 : move left, 1 : move right 
                int next_pos_delta_y = _random.Next(-1, 2);

                int next_pos_x = pos._x + next_pos_delta_x;
                int next_pos_y = pos._y + next_pos_delta_y;

                if ((next_pos_delta_x == 0 && next_pos_delta_y == 0) || (false == CheckTile(next_pos_x, next_pos_y)))
                {
                    return ProcessFakeInputMove(checked_count+1);
                }

                cs_move packet = new cs_move();
                packet._x = next_pos_x;
                packet._y = next_pos_y;

                Debug.Log($"cs_move => x : {packet._x}, y : {packet._y}");

                GameManager.CLIENT.Send(packet);

                return true;
            }

            return false;
        }

        private bool GetCurrentPos(out int out_x, out int out_y, int object_id)
        {
            Pos pos;

            if (_object_info.TryGetValue(object_id, out pos))
            {
                out_x = pos._x;
                out_y = pos._y;
                return true;
            }
            else
            {
                out_x = 0;
                out_y = 0;
                return false;
            }
        }

        private void SetAndDrawPlayer(int client_id, int x, int y, Color color)
        {
            if (CheckTile(x, y))
            {
                Player player = new Player(client_id);

                SetObject(x, y, player);

                DrawRequest drawRequest = new DrawRequest();
                drawRequest.SetDataForCreate(x, y, client_id, color);
                
                GameManager.PushDrawRequest(drawRequest);

                Debug.Log($"SetAndDrawPlayer() => id : {client_id}, x : {x}, y : {y}");
            }
            else
            {
                Debug.LogError($"SetAndDrawPlayer() => CheckTile() failed. id : {client_id}, x : {x}, y : {y}");
            }
        }
    }
}
