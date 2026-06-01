using Fusion;
using EyeMoT.Fusion;
using UnityEngine;

namespace EyeMoT.Balloon
{
    public class BalloonInputProvider : InputProvider
    {
        private bool _isButtonPush;

        private void Update()
        {
            _isButtonPush |= DetectButtonPush();
        }

        public virtual BalloonNetworkInput CreateInput()
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            Vector3 mouse = Input.mousePosition;

            BalloonNetworkInput input = new BalloonNetworkInput
            {
                HasMouse = mouse.x >= 0f && mouse.x <= width && mouse.y >= 0f && mouse.y <= height,
                MouseUV = new Vector2(
                    Mathf.Clamp01(mouse.x / width),
                    Mathf.Clamp01(mouse.y / height)),
                ScreenAspect = (float)width / height,
                IsButtonPush = _isButtonPush
            };

            _isButtonPush = false;
            return input;
        }

        private static bool DetectButtonPush()
        {
            return
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetMouseButtonDown(0) ||
                Input.GetKeyDown(KeyCode.JoystickButton0) ||
                Input.GetKeyDown(KeyCode.JoystickButton1) ||
                Input.GetKeyDown(KeyCode.JoystickButton2) ||
                Input.GetKeyDown(KeyCode.JoystickButton3) ||
                Input.GetKeyDown(KeyCode.JoystickButton4) ||
                Input.GetKeyDown(KeyCode.JoystickButton5) ||
                Input.GetKeyDown(KeyCode.JoystickButton6) ||
                Input.GetKeyDown(KeyCode.JoystickButton7) ||
                Input.GetKeyDown(KeyCode.JoystickButton8) ||
                Input.GetKeyDown(KeyCode.JoystickButton9) ||
                Input.GetKeyDown(KeyCode.JoystickButton10) ||
                Input.GetKeyDown(KeyCode.JoystickButton11);
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
