using System;
using System.Text;
using UnityEngine.SceneManagement;

namespace EyeMoT.Fusion
{
    public static class SessionCodeUtility
    {
        const string JoinTokenPrefix = "JOIN";

        public static string BuildSessionCode(SessionDef.Name sessionName)
        {
            return $"{SceneManager.GetActiveScene().name}_{sessionName}";
        }

        public static SessionDef.Name ParseSessionName(string sessionCode)
        {
            string scenePrefix = SceneManager.GetActiveScene().name + "_";
            string rawName = sessionCode.Replace(scenePrefix, "");
            return (SessionDef.Name)Enum.Parse(typeof(SessionDef.Name), rawName);
        }

        public static byte[] BuildJoinToken(string sessionCode)
        {
            return Encoding.UTF8.GetBytes($"{JoinTokenPrefix}:{sessionCode}");
        }

        public static bool IsValidJoinToken(byte[] token, string expectedSessionCode)
        {
            if (token == null || token.Length == 0)
            {
                return false;
            }

            string payload = Encoding.UTF8.GetString(token);
            string[] parts = payload.Split(':');

            return parts.Length == 2 &&
                   parts[0] == JoinTokenPrefix &&
                   parts[1] == expectedSessionCode;
        }
    }
}
