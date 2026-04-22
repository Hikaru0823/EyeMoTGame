using System;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EyeMoT.Fusion;

public class DisconnectUI : MonoBehaviour
{
	public static DisconnectUI Instance { get; private set; }

	[Header("Resources")]

	[SerializeField] private Canvas _ui;
	[SerializeField] private TMP_Text _disconnectStatus;
	[SerializeField] private TMP_Text _disconnectMessage;
	[SerializeField] private Button _closeButton;

	private void Awake()
	{
		if (Instance)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
	}

	void Start()
	{
		_closeButton.onClick.AddListener(() =>
		{
			Instance._ui.enabled = false;
			LobbyManager.Instance.Quit();
		});
	}

	public static void OnShutdown(ShutdownReason reason)
	{
		Debug.Log($"<color=orange>[Fusion]</color> {reason}");
		if (reason == ShutdownReason.Ok) return;

		(string status, string message) = ShutdownReasonToHuman(reason);
		
		Instance._disconnectStatus.text = status;
		Instance._disconnectMessage.text = message;

		Instance._ui.enabled = true;
		Cursor.lockState = CursorLockMode.None;
	}

	public static void OnConnectFailed(NetConnectFailedReason reason)
	{
		Debug.Log($"<color=orange>[Fusion]</color> {reason}");
		(string status, string message) = ConnectFailedReasonToHuman(reason);

		Instance._disconnectStatus.text = status;
		Instance._disconnectMessage.text = message;

		Instance._ui.enabled = true;
		Cursor.lockState = CursorLockMode.None;
	}

	public static void OnDisconnectedFromServer(NetDisconnectReason reason)
	{
		(string status, string message) = DisconnectReasonToHuman(reason);
		Instance._disconnectStatus.text = status;
		Instance._disconnectMessage.text = message;

		Instance._ui.enabled = true;
		Cursor.lockState = CursorLockMode.None;
	}

	private static (string, string) ShutdownReasonToHuman(ShutdownReason reason)
	{
		switch (reason)
		{
			case ShutdownReason.Ok:
				return ("ホスト退出", "ホストが退出しました");
			case ShutdownReason.Error:
				return ("エラー", "内部エラーによりシャットダウンしました");
			case ShutdownReason.IncompatibleConfiguration:
				return ("設定不一致", "クライアント・サーバーモードと共有モードが一致しません");
			case ShutdownReason.ServerInRoom:
				return ("ルーム使用中", "そのルームは既に使用されています。別のルームを試すか、しばらく待ってください。");
			case ShutdownReason.DisconnectedByPluginLogic:
				return ("切断", "キックされました。ルームが閉じられた可能性があります");
			case ShutdownReason.GameClosed:
				return ("ゲーム終了", "セッションに参加できません。ゲームが終了しています");
			case ShutdownReason.GameNotFound:
				return ("ゲームが見つかりません", "このルームは存在しません");
			case ShutdownReason.MaxCcuReached:
				return ("最大プレイヤー数到達", "最大同時接続数に達しました。後でお試しください");
			case ShutdownReason.InvalidRegion:
				return ("無効なリージョン", "現在選択されているリージョンが無効です");
			case ShutdownReason.GameIdAlreadyExists:
				return ("ID既存", "この名前のルームは既に作成されています");
			case ShutdownReason.GameIsFull:
				return ("ゲーム満員", "このセッションは満員です！");
			case ShutdownReason.InvalidAuthentication:
				return ("認証無効", "認証値が無効です");
			case ShutdownReason.CustomAuthenticationFailed:
				return ("認証失敗", "カスタム認証に失敗しました");
			case ShutdownReason.AuthenticationTicketExpired:
				return ("認証期限切れ", "認証チケットの有効期限が切れました");
			case ShutdownReason.PhotonCloudTimeout:
				return ("切断", "ホストとの接続が切断されました");
			case ShutdownReason.AlreadyRunning:
				return ("既に実行中", "接続が既に実行されています");
			case ShutdownReason.InvalidArguments:
				return ("無効な引数", "StartGameの引数が無効です");
			case ShutdownReason.HostMigration:
				return ("ホスト移行", "ホストが移行中です");
			case ShutdownReason.ConnectionTimeout:
				return ("タイムアウト", "リモートサーバーとの接続がタイムアウトしました");
			case ShutdownReason.ConnectionRefused:
				return ("接続拒否", "リモートサーバーが接続を拒否しました");
			default:
				Debug.LogWarning($"不明なShutdownReason {reason}");
				return ("不明なシャットダウン理由", $"{(int)reason}");
		}
	}

	private static (string,string) ConnectFailedReasonToHuman(NetConnectFailedReason reason)
	{
		switch (reason)
		{
			case NetConnectFailedReason.Timeout:
				return ("タイムアウト", "");
			case NetConnectFailedReason.ServerRefused:
				return ("接続拒否", "セッションが現在ゲーム中の可能性があります");
			case NetConnectFailedReason.ServerFull:
				return ("サーバー満員", "");
			default:
				Debug.LogWarning($"不明なNetConnectFailedReason {reason}");
				return ("不明な接続失敗", $"{(int)reason}");
		}
	}

	private static (string,string) DisconnectReasonToHuman(NetDisconnectReason reason)
	{
		switch (reason)
		{
			case NetDisconnectReason.Timeout:
				return ("タイムアウト", "");
			case NetDisconnectReason.Requested:
				return ("切断要求", "");
			case NetDisconnectReason.SequenceOutOfBounds:
				return ("サーバー満員", "");
			case NetDisconnectReason.SendWindowFull:
				return ("送信ウィンドウ満杯", "");
			case NetDisconnectReason.ByRemote:
				return ("リモートによる切断", "");
			default:
				Debug.LogWarning($"不明なNetDisconnectReason {reason}");
				return ("不明な切断理由", $"{(int)reason}");
		}
	}
}