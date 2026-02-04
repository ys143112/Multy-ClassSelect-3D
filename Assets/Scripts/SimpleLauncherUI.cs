using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class SimpleLauncherUI : MonoBehaviour
{
    public string address = "127.0.0.1";
    public ushort port = 7777;

    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            GUI.Label(new Rect(10, 10, 400, 30), "NetworkManager.Singleton이 없음");
            return;
        }

        // 이미 시작됐으면 상태만 표시
        if (nm.IsClient || nm.IsServer)
        {
            string mode = nm.IsHost ? "HOST" : (nm.IsServer ? "SERVER" : "CLIENT");
            GUI.Label(new Rect(10, 10, 300, 30), $"Net: {mode}");
            return;
        }

        GUI.Label(new Rect(10, 10, 70, 25), "IP");
        address = GUI.TextField(new Rect(80, 10, 160, 25), address);

        GUI.Label(new Rect(10, 40, 70, 25), "Port");
        string portStr = GUI.TextField(new Rect(80, 40, 160, 25), port.ToString());
        if (ushort.TryParse(portStr, out var p)) port = p;

        if (GUI.Button(new Rect(10, 80, 230, 40), "Start Host"))
        {
            SetTransport(address, port);
            nm.StartHost();
        }

        if (GUI.Button(new Rect(10, 130, 230, 40), "Join as Client"))
        {
            SetTransport(address, port);
            nm.StartClient();
        }
    }

    void SetTransport(string addr, ushort prt)
    {
        var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (utp != null)
            utp.SetConnectionData(addr, prt);
    }
}
