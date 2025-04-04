using Unity.Netcode;
using UnityEngine;

public class SingletonManager<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool HasInstance => _instance; //싱글톤 인스턴스가 존재하는지 여부를 확인한다.
    public static T TryGetInstance() => HasInstance ? _instance : null; //인스턴스를 강제로 생성하지 않고, 이미 존재하는 인스턴스에 접근하고자 할 때 쓴다. (인스턴스가 없다면 null을 반환한다.)
    public static T Current => _instance; //인스턴스를 생성하지 않고 인스턴스에 접근하려고 할 때 쓴다.

    public static T Instance
    {
        get
        {
            if (!_instance)
            {
                _instance = FindFirstObjectByType<T>();
                if (!_instance)
                {
                    GameObject obj = new GameObject
                    {
                        name = typeof(T).Name + "_AutoCreated"
                    };
                    _instance = obj.AddComponent<T>();
                }
            }

            return _instance;
        }
    }

    //Awake에서 인스턴스를 초기화한다. 만약 awake를 override해서 사용해야 한다면 base.Awake()를 호출해야 한다.
    protected virtual void Awake()
    {
        InitializeSingleton();
    }

    protected virtual void InitializeSingleton()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        _instance = this as T;
    }
}

public class NetworkSingletonManager<T> : NetworkBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool HasInstance => _instance; //싱글톤 인스턴스가 존재하는지 여부를 확인한다.
    public static T TryGetInstance() => HasInstance ? _instance : null; //인스턴스를 강제로 생성하지 않고, 이미 존재하는 인스턴스에 접근하고자 할 때 쓴다. (인스턴스가 없다면 null을 반환한다.)
    public static T Current => _instance; //인스턴스를 생성하지 않고 인스턴스에 접근하려고 할 때 쓴다.

    public static T Instance
    {
        get
        {
            if (!_instance)
            {
                _instance = FindFirstObjectByType<T>();
                if (!_instance)
                {
                    GameObject obj = new GameObject
                    {
                        name = typeof(T).Name + "_AutoCreated"
                    };
                    _instance = obj.AddComponent<T>();
                }
            }

            return _instance;
        }
    }

    //Awake에서 인스턴스를 초기화한다. 만약 awake를 override해서 사용해야 한다면 base.Awake()를 호출해야 한다.
    protected virtual void Awake()
    {
        InitializeSingleton();
    }

    protected virtual void InitializeSingleton()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        _instance = this as T;
    }
}