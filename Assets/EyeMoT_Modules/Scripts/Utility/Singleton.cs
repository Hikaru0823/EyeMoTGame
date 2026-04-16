using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : Component
{
    private static T instance;

    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = (T)FindFirstObjectByType(typeof(T));
                if (instance == null)
                {
                    SetupInstance();
                }
            }
            return instance;
        }
    }

    protected void Awake()
    {
        if (!TryRegisterInstance())
        {
            return;
        }

        OnAwake();
    }

    protected virtual void OnAwake()
    {
    }

    private static void SetupInstance()
    {
        instance = (T)FindFirstObjectByType(typeof(T));
        if (instance == null)
        {
            GameObject gameObj = new GameObject();
            gameObj.name = typeof(T).Name;
            instance = gameObj.AddComponent<T>();
            DontDestroyOnLoad(gameObj);
        }
    }

    private bool TryRegisterInstance()
    {
        if (instance == null)
        {
            instance = this as T;
            DontDestroyOnLoad(gameObject);
            return true;
        }

        if (instance != this)
        {
            Destroy(gameObject);
            return false;
        }

        return true;
    }
}

public class SceneSingleton<T> : MonoBehaviour where T : Component
{
    private static T instance;

    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = (T)FindFirstObjectByType(typeof(T));
                if (instance == null)
                {
                    SetupInstance();
                }
            }
            return instance;
        }
    }

    protected void Awake()
    {
        if (!TryRegisterInstance())
        {
            return;
        }

        OnAwake();
    }

    protected virtual void OnAwake()
    {
    }

    private static void SetupInstance()
    {
        instance = (T)FindFirstObjectByType(typeof(T));
        if (instance == null)
        {
            GameObject gameObj = new GameObject();
            gameObj.name = typeof(T).Name;
            instance = gameObj.AddComponent<T>();
        }
    }

    private bool TryRegisterInstance()
    {
        if (instance == null)
        {
            instance = this as T;
            return true;
        }

        if (instance != this)
        {
            Destroy(gameObject);
            return false;
        }

        return true;
    }
}
