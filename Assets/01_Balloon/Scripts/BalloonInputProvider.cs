using Fusion;
using EyeMoT.Fusion;
using UnityEngine;

namespace EyeMoT.Balloon
{
    public class BalloonInputProvider : InputProvider
    {
        public virtual BalloonNetworkInput CreateInput()
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            Vector3 mouse = Input.mousePosition;

            return new BalloonNetworkInput
            {
                HasMouse = mouse.x >= 0f && mouse.x <= width && mouse.y >= 0f && mouse.y <= height,
                MouseUV = new Vector2(
                    Mathf.Clamp01(mouse.x / width),
                    Mathf.Clamp01(mouse.y / height)),
                ScreenAspect = (float)width / height
            };
        }

        public virtual BalloonNetworkInput CreateMissingInput()
        {
            return new BalloonNetworkInput
            {
                HasMouse = false,
                MouseUV = new Vector2(0.5f, 0.5f),
                ScreenAspect = Screen.width > 0 && Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f
            };
        }

        public override void ApplyInput(NetworkInput input)
        {
            input.Set(CreateInput());
        }

        public override void ApplyMissingInput(NetworkInput input)
        {
            input.Set(CreateMissingInput());
        }
    }
}
