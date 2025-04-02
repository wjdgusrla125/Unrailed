using Unity.Netcode;
using UnityEngine;

public class ConnectServer : MonoBehaviour
{
    public void ConnectToServer()
    {
        NetworkManager.Singleton.StartClient();
    }

    public void ConnectToHost()
    {
        NetworkManager.Singleton.StartHost();
    }
}
