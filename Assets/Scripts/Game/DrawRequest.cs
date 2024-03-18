using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Game
{
    public class DrawRequest
    {
        public enum RequestID : int
        {
            none,
            create,
            move,
            remove
        }

        public RequestID _id;
        public int _x;
        public int _y;
        public int _client_id;
        public Color _color;

        public void SetDataForCreate(int x, int y, int client_id, Color color)
        {
            _id = RequestID.create;
            _x = x;
            _y = y;
            _client_id = client_id;
            _color = color;
        }

        public void SetDataForMove(int x, int y, int client_id)
        {
            _id = RequestID.move;
            _x = x;
            _y = y;
            _client_id = client_id;
        }

        public void SetDataForRemove(int client_id)
        {
            _id = RequestID.remove;
            _client_id = client_id;
        }
    }
}
