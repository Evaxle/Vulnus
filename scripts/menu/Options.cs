using Godot;
using System;
using System.Threading.Tasks;

public class Options : View
{
	public bool CanOpen = true;
	private bool moving = false;
	private string pendingDeviceCode;
	private ulong loginDeadline;
	private ulong nextLoginPoll;
	private Task<bool> mapSyncTask;
	private Control accountPanel;
	private LineEdit codeField;
	private Button loginButton;
	private Label accountStatus;
	private Label accountMessage;

	public override void _Ready()
	{
		base._Ready();
		var topbar = GetNode<Control>("Topbar");
		var closeBtn = topbar.GetNode<Button>("Close");
		closeBtn.Connect("pressed", this, nameof(SetActive), new Godot.Collections.Array(false));
		accountPanel = GetNode<Control>("Content/Account");
		accountStatus = accountPanel.GetNode<Label>("Status");
		accountMessage = accountPanel.GetNode<Label>("Unavailable");
		var login = accountPanel.GetNode<VBoxContainer>("Login");
		codeField = login.GetNode<LineEdit>("Username");
		codeField.Editable = false;
		codeField.PlaceholderText = "Rhythians authorization code";
		login.GetNode<CheckBox>("Remember").Visible = false;
		login.GetNode<LineEdit>("Password").Visible = false;
		loginButton = login.GetNode<Button>("Login");
		loginButton.Connect("pressed", this, nameof(RhythiansLoginPressed));
		UpdateAccountUi();
	}

	public override void _PhysicsProcess(float delta)
	{
		if (pendingDeviceCode != null)
		{
			var now = OS.GetTicksMsec();
			if (now >= loginDeadline)
			{
				pendingDeviceCode = null;
				accountMessage.Text = "Rhythians authorization expired. Start login again.";
				accountMessage.Visible = true;
				UpdateAccountUi();
			}
			else if (now >= nextLoginPoll)
			{
				nextLoginPoll = now + 1500;
				bool pending;
				if (RhythiansApi.PollDeviceLogin(pendingDeviceCode, out pending))
				{
					pendingDeviceCode = null;
					accountMessage.Text = "Logged in. Syncing Rhythians maps...";
					accountMessage.Visible = true;
					mapSyncTask = Task.Run(() => RhythiansApi.SyncMaps(true));
					UpdateAccountUi();
				}
				else if (!pending && !string.IsNullOrWhiteSpace(RhythiansApi.LastError))
				{
					pendingDeviceCode = null;
					accountMessage.Text = RhythiansApi.LastError;
					accountMessage.Visible = true;
					UpdateAccountUi();
				}
			}
		}
		if (mapSyncTask != null && mapSyncTask.IsCompleted)
		{
			var success = !mapSyncTask.IsFaulted && mapSyncTask.Result;
			mapSyncTask = null;
			accountMessage.Text = success ? "Rhythians maps are synced." : RhythiansApi.LastError ?? "Map sync failed.";
			accountMessage.Visible = true;
			UpdateAccountUi();
		}
		if (CanOpen && Input.IsActionJustPressed("options"))
			SetActive(!IsActive);
		if (!IsActive)
			return;
		bool rankable = true;
		if (Settings.ApproachRate > 100f)
			rankable = false;
		if (Settings.ApproachDistance > 100f)
			rankable = false;
		if (Settings.ApproachTime > 2f)
			rankable = false;
		GetNode<Label>("RankStatus").Text = rankable ? "These settings are rankable." : "These settings are not rankable.";
	}

	public void RhythiansLoginPressed()
	{
		if (RhythiansApi.IsAuthenticated)
		{
			RhythiansApi.Logout();
			pendingDeviceCode = null;
			codeField.Text = "";
			accountMessage.Text = "Logged out of Rhythians.";
			accountMessage.Visible = true;
			UpdateAccountUi();
			return;
		}
		var login = RhythiansApi.StartDeviceLogin();
		if (login == null)
		{
			accountMessage.Text = RhythiansApi.LastError ?? "Unable to start Rhythians login.";
			accountMessage.Visible = true;
			return;
		}
		pendingDeviceCode = login.DeviceCode;
		loginDeadline = OS.GetTicksMsec() + (ulong)Math.Max(30, login.ExpiresIn) * 1000;
		nextLoginPoll = OS.GetTicksMsec() + 1000;
		codeField.Text = login.UserCode ?? "";
		accountStatus.Text = "Approve this Vulnus login on rhythians.com.";
		accountMessage.Text = "Code: " + login.UserCode;
		accountMessage.Visible = true;
		loginButton.Text = "WAITING FOR APPROVAL";
		OS.ShellOpen(login.VerificationUrl);
	}

	private void UpdateAccountUi()
	{
		if (RhythiansApi.IsAuthenticated)
		{
			accountStatus.Text = "Logged in to Rhythians as " + (RhythiansApi.Username ?? "user") + ".";
			loginButton.Text = mapSyncTask == null ? "LOG OUT" : "SYNCING MAPS...";
			loginButton.Disabled = mapSyncTask != null;
			if (codeField != null)
				codeField.Text = RhythiansApi.Username ?? "";
		}
		else
		{
			accountStatus.Text = pendingDeviceCode == null ? "Log in with your rhythians.com account." : "Waiting for Rhythians authorization.";
			loginButton.Text = pendingDeviceCode == null ? "LOGIN WITH RHYTHIANS" : "WAITING FOR APPROVAL";
			loginButton.Disabled = pendingDeviceCode != null;
		}
	}

	public override async void OnShow()
	{
		if (moving || IsActive)
			return;
		moving = true;
		UpdateAccountUi();
		this.Visible = true;
		ViewTween.InterpolateProperty(this, "modulate:a", 0, 1, 0.15f, Tween.TransitionType.Sine);
		ViewTween.InterpolateProperty(this, "rect_scale", new Vector2(0.8f, 0.8f), new Vector2(1, 1), 0.2f, Tween.TransitionType.Sine, Tween.EaseType.Out);
		ViewTween.Start();
		await ToSignal(ViewTween, "tween_all_completed");
		IsActive = true;
		moving = false;
	}

	public override async void OnHide()
	{
		if (moving || !IsActive)
			return;
		moving = true;
		ViewTween.InterpolateProperty(this, "modulate:a", 1, 0, 0.15f, Tween.TransitionType.Sine);
		ViewTween.InterpolateProperty(this, "rect_scale", new Vector2(1, 1), new Vector2(0.9f, 0.9f), 0.2f, Tween.TransitionType.Sine, Tween.EaseType.Out);
		ViewTween.Start();
		await ToSignal(ViewTween, "tween_all_completed");
		IsActive = false;
		this.Visible = false;
		moving = false;
	}
}
