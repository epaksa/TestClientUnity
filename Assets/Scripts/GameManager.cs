using Assets.Scripts.Common.Network;
using Assets.Scripts.Game;
using Assets.Scripts.Game.Zone;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static readonly string SERVER_IP = "localhost";
    public static readonly int SERVER_PORT = 19001;

    public static readonly int SEND_BUFFER_SIZE = 10240;
    public static readonly int READ_BUFFER_SIZE = 10240;

    public static readonly string MAP_FILE_NAME = "map.csv";
    
    public static readonly string PLAYER_OBJECT_NAME_PREFIX = "PlayerObject - ";

    public static Zone? ZONE = null;
    public static Client? CLIENT = null;
    
    private static Dictionary<int, GameObject> PLAYER_OBJECT_MAP = new Dictionary<int, GameObject>();
    private static ConcurrentQueue<DrawRequest> DRAW_REQUEST_QUEUE = new ConcurrentQueue<DrawRequest>();
    private static Dictionary<int, Vector3> DRAW_IN_PROGRESS_MAP = new Dictionary<int, Vector3>(); // key : client_id, value : destination
    private static List<int> LIST_FINISHED_CLIENT_ID = new List<int>();

    private void Awake()
    {
        ZONE = new Zone(MAP_FILE_NAME);
        ZONE.Start();

        CLIENT = new Client();
        CLIENT.Connect(SERVER_IP, SERVER_PORT);
    }

    private void OnDestroy()
    {
        ZONE.Stop();

        CLIENT.Close();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // zone thread에서 main thread로 ui update 요청한거 처리
        while (DRAW_REQUEST_QUEUE.TryDequeue(out DrawRequest? drawRequest))
        {
            HandleDrawRequest(drawRequest);
        }

        // 이동 처리
        LIST_FINISHED_CLIENT_ID.Clear();
        
        foreach (KeyValuePair<int, Vector3> pair in DRAW_IN_PROGRESS_MAP)
        {
            bool finished = Draw(pair.Key, pair.Value);

            if (finished)
            {
                LIST_FINISHED_CLIENT_ID.Add(pair.Key);
            }
        }

        foreach (int key in LIST_FINISHED_CLIENT_ID)
        {
            DRAW_IN_PROGRESS_MAP.Remove(key);
        }
    }

    public static void PushDrawRequest(DrawRequest drawRequest)
    {
        DRAW_REQUEST_QUEUE.Enqueue(drawRequest);
    }

    private void HandleDrawRequest(DrawRequest drawRequest)
    {
        switch (drawRequest._id)
        {
            case DrawRequest.RequestID.create:
            {
                GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = PLAYER_OBJECT_NAME_PREFIX + drawRequest._client_id;
                obj.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                obj.transform.position = ConvertPositionToVector(drawRequest._x, drawRequest._y);
                obj.GetComponent<Renderer>().material.color = drawRequest._color;

                PLAYER_OBJECT_MAP.Add(drawRequest._client_id, obj);

                break;
            }
            case DrawRequest.RequestID.move:
            {
                Vector3 destination;
                if (DRAW_IN_PROGRESS_MAP.TryGetValue(drawRequest._client_id, out destination))
                {
                    destination = ConvertPositionToVector(drawRequest._x, drawRequest._y);
                }
                else
                {
                    destination = ConvertPositionToVector(drawRequest._x, drawRequest._y);
                    DRAW_IN_PROGRESS_MAP.Add(drawRequest._client_id, destination);
                }

                break;
            }
            case DrawRequest.RequestID.remove:
            {
                GameObject obj;
                if (PLAYER_OBJECT_MAP.TryGetValue(drawRequest._client_id, out obj))
                {
                    Destroy(obj);
                }

                DRAW_IN_PROGRESS_MAP.Remove(drawRequest._client_id);

                break;
            }
            default:
                break;
        }
    }

    private bool Draw(int client_id, Vector3 destination)
    {
        bool finished = false;

        GameObject obj;
        
        if (PLAYER_OBJECT_MAP.TryGetValue(client_id, out obj))
        {
            obj.transform.position = Vector3.MoveTowards(obj.transform.position, destination, Time.deltaTime * 2);

            if (obj.transform.position == destination)
            {
                finished = true;
            }
        }
        else
        {
            Debug.LogError($"not found in PLAYER_OBJECT_MAP. client id : {client_id}");
        }

        return finished;
    }

    private Vector3 ConvertPositionToVector(int x, int y)
    {
        return new Vector3((x - ZONE.GetCountX(y) / 2) + 0.5f, 1.0f / 2.0f, -(y - (ZONE.GetCountY() / 2)) - 1 + 0.5f);
    }
}
