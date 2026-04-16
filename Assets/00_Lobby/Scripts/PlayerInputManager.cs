using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EyeMoT.Fusion
{
	public class PlayerInputManager : MonoBehaviour
	{
		[SerializeField] private string blockTag = "Block UI";
		void Update()
		{
			if (PlayerObject.Local == null || PlayerObject.Local.Controller == null) return;

			if (DetectAnyInput())
			{
				if(GetUITagUnderMouse() == blockTag) return;
				PlayerObject.Local.Rpc_OnInput();
			}
		}

		/// <summary>
		/// マウス下のUIのタグを取得する
		/// </summary>
		/// <returns>マウス下のUIのタグ。UIがない場合はnull</returns>
		public string GetUITagUnderMouse()
		{
			PointerEventData pointerData = new PointerEventData(EventSystem.current);
			pointerData.position = Input.mousePosition;

			List<RaycastResult> results = new List<RaycastResult>();
			EventSystem.current.RaycastAll(pointerData, results);

			if (results.Count > 0)
			{
				Debug.Log($"UI Tag under mouse: {results[0].gameObject.tag}");
				return results[0].gameObject.tag;
			}

			return null;
		}

		/// <summary>
		/// 任意の入力を検知して共通の処理を実行
		/// スペース、エンター、左クリック、ゲームパッドの全ての入力が同じ処理をトリガー
		/// </summary>
		private bool DetectAnyInput()
		{
			// キーボード入力検知
			if (Input.GetKeyDown(KeyCode.Space) ||
				Input.GetKeyDown(KeyCode.Return) ||
				Input.GetKeyDown(KeyCode.KeypadEnter))
			{
				 return true;
			}

			// マウス入力検知
			if (Input.GetMouseButtonDown(0))
			{
				return true;
			}

			// ゲームパッド入力検知（主要ボタン）
			if (Input.GetKeyDown(KeyCode.JoystickButton0) ||  // A/X ボタン
				Input.GetKeyDown(KeyCode.JoystickButton1) ||  // B/○ ボタン
				Input.GetKeyDown(KeyCode.JoystickButton2) ||  // X/□ ボタン
				Input.GetKeyDown(KeyCode.JoystickButton3) ||  // Y/△ ボタン
				Input.GetKeyDown(KeyCode.JoystickButton4) ||  // L1/L ボタン
				Input.GetKeyDown(KeyCode.JoystickButton5) ||  // R1/R ボタン
				Input.GetKeyDown(KeyCode.JoystickButton6) ||  // L2/ZL ボタン
				Input.GetKeyDown(KeyCode.JoystickButton7) ||   // Start ボタン
				Input.GetKeyDown(KeyCode.JoystickButton8) ||   // Select ボタン
				Input.GetKeyDown(KeyCode.JoystickButton9) ||   // R2/ZR ボタン
				Input.GetKeyDown(KeyCode.JoystickButton10) ||  // Left Stick ボタン
				Input.GetKeyDown(KeyCode.JoystickButton11))    // Right Stick ボタン
			{
				return true;
			}

			return false;
		}
	}
}