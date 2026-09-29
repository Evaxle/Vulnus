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
	private LineEdit noteColorA;
	private LineEdit noteColorB;
	private LineEdit cursorColor;
	private SpinBox noteScale;
	private SpinBox noteOpacity;
	private SpinBox cursorScale;
	private OptionButton cursorPreset;
	private OptionButton colorPreset;
	private OptionButton settingsPreset;
	private LineEdit presetName;

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
		BuildCustomizationTab();
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
					if (!RhythiansApi.HasLinkedRhythia)
					{
						accountMessage.Text = "Vulnus is connected, but this Rhythians account must link a Rhythia Steam profile on rhythians.com before maps and scores are available.";
						accountMessage.Visible = true;
						UpdateAccountUi();
					}
					else
					{
						accountMessage.Text = "Logged in. Syncing the Rhythians catalog...";
						accountMessage.Visible = true;
						mapSyncTask = Task.Run(() => RhythiansApi.SyncMaps(false));
						UpdateAccountUi();
					}
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
			accountMessage.Text = success ? "Rhythians catalog is ready. Open Maps Catalog to install maps." : RhythiansApi.LastError ?? "Catalog sync failed.";
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
			accountStatus.Text = "Logged in to Rhythians as " + (RhythiansApi.Username ?? "user") + "." + (RhythiansApi.HasLinkedRhythia ? " Rhythia Steam profile linked." : " Link a Rhythia Steam profile on rhythians.com to unlock maps and score submission.");
			loginButton.Text = mapSyncTask == null ? "LOG OUT" : "SYNCING CATALOG...";
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

	private void BuildCustomizationTab()
	{
		var tabs = GetNode<TabContainer>("Content");
		var scroll = new ScrollContainer();
		scroll.Name = "Customization";
		scroll.AnchorRight = 1f;
		scroll.AnchorBottom = 1f;
		scroll.SizeFlagsHorizontal = 3;
		scroll.SizeFlagsVertical = 3;
		tabs.AddChild(scroll);

		var body = new VBoxContainer();
		body.SizeFlagsHorizontal = 3;
		body.AddConstantOverride("separation", 8);
		scroll.AddChild(body);

		var heading = new Label();
		heading.Text = "Notes, Cursor & Presets";
		heading.RectMinSize = new Vector2(0, 36);
		body.AddChild(heading);

		noteScale = AddNumberRow(body, "Note Size", 0.25, 3, 0.05, Settings.NoteScale, nameof(OnNoteScaleChanged));
		noteOpacity = AddNumberRow(body, "Note Opacity", 0, 100, 1, Settings.NoteOpacity * 100f, nameof(OnNoteOpacityChanged), "%");
		noteColorA = AddTextRow(body, "Note Color A", Settings.NoteColorA, nameof(OnNoteColorAChanged));
		noteColorB = AddTextRow(body, "Note Color B", Settings.NoteColorB, nameof(OnNoteColorBChanged));
		cursorScale = AddNumberRow(body, "Cursor Size", 0.1, 5, 0.05, Settings.CursorScale, nameof(OnCursorScaleChanged));
		cursorColor = AddTextRow(body, "Cursor Color", Settings.CursorColor, nameof(OnCursorColorChanged));

		cursorPreset = AddDropdownRow(body, "Downloaded Cursor", nameof(OnCursorPresetChanged));
		colorPreset = AddDropdownRow(body, "Color Preset", nameof(OnColorPresetChanged));
		settingsPreset = AddDropdownRow(body, "Settings Preset", nameof(OnSettingsPresetChanged));

		var presetRow = new HBoxContainer();
		presetRow.AddConstantOverride("separation", 8);
		body.AddChild(presetRow);
		presetName = new LineEdit();
		presetName.PlaceholderText = "Preset name";
		presetName.SizeFlagsHorizontal = 3;
		presetRow.AddChild(presetName);
		var save = new Button();
		save.Text = "SAVE PRESET";
		save.Connect("pressed", this, nameof(OnSavePreset));
		presetRow.AddChild(save);
		var openFolder = new Button();
		openFolder.Text = "OPEN ASSET FOLDERS";
		openFolder.Connect("pressed", this, nameof(OnOpenPresetFolders));
		presetRow.AddChild(openFolder);
		RefreshCustomizationLists();
	}

	private SpinBox AddNumberRow(VBoxContainer parent, string title, double min, double max, double step, double value, string method, string suffix = "")
	{
		var row = new HBoxContainer();
		row.RectMinSize = new Vector2(0, 34);
		parent.AddChild(row);
		var label = new Label();
		label.Text = title;
		label.SizeFlagsHorizontal = 3;
		label.Valign = Label.VAlign.Center;
		row.AddChild(label);
		var input = new SpinBox();
		input.MinValue = min;
		input.MaxValue = max;
		input.Step = step;
		input.Value = value;
		input.Suffix = suffix;
		input.RectMinSize = new Vector2(170, 32);
		input.Connect("value_changed", this, method);
		row.AddChild(input);
		return input;
	}

	private LineEdit AddTextRow(VBoxContainer parent, string title, string value, string method)
	{
		var row = new HBoxContainer();
		row.RectMinSize = new Vector2(0, 34);
		parent.AddChild(row);
		var label = new Label();
		label.Text = title;
		label.SizeFlagsHorizontal = 3;
		label.Valign = Label.VAlign.Center;
		row.AddChild(label);
		var input = new LineEdit();
		input.Text = value;
		input.RectMinSize = new Vector2(170, 32);
		input.Connect("text_entered", this, method);
		input.Connect("focus_exited", this, method + "Focus");
		row.AddChild(input);
		return input;
	}

	private OptionButton AddDropdownRow(VBoxContainer parent, string title, string method)
	{
		var row = new HBoxContainer();
		row.RectMinSize = new Vector2(0, 34);
		parent.AddChild(row);
		var label = new Label();
		label.Text = title;
		label.SizeFlagsHorizontal = 3;
		label.Valign = Label.VAlign.Center;
		row.AddChild(label);
		var input = new OptionButton();
		input.RectMinSize = new Vector2(260, 32);
		input.Connect("item_selected", this, method);
		row.AddChild(input);
		return input;
	}

	private void RefreshCustomizationLists()
	{
		if (cursorPreset != null)
		{
			cursorPreset.Clear();
			cursorPreset.AddItem("Default cursor");
			foreach (var path in Settings.GetCursorFiles()) cursorPreset.AddItem(System.IO.Path.GetFileName(path));
		}
		if (colorPreset != null)
		{
			colorPreset.Clear();
			colorPreset.AddItem("Current colors");
			foreach (var path in Settings.GetColorPresetFiles()) colorPreset.AddItem(System.IO.Path.GetFileNameWithoutExtension(path));
		}
		if (settingsPreset != null)
		{
			settingsPreset.Clear();
			settingsPreset.AddItem("Current settings");
			foreach (var name in Settings.GetPresetNames()) settingsPreset.AddItem(name);
		}
	}

	private void RefreshCustomizationValues()
	{
		if (noteScale != null) noteScale.Value = Settings.NoteScale;
		if (noteOpacity != null) noteOpacity.Value = Settings.NoteOpacity * 100f;
		if (noteColorA != null) noteColorA.Text = Settings.NoteColorA;
		if (noteColorB != null) noteColorB.Text = Settings.NoteColorB;
		if (cursorScale != null) cursorScale.Value = Settings.CursorScale;
		if (cursorColor != null) cursorColor.Text = Settings.CursorColor;
	}

	public void OnNoteScaleChanged(float value) { Settings.NoteScale = value; Settings.UpdateSettings(); }
	public void OnNoteOpacityChanged(float value) { Settings.NoteOpacity = value / 100f; Settings.UpdateSettings(); }
	public void OnNoteColorAChanged(string value) { Settings.NoteColorA = value; Settings.UpdateSettings(); RefreshCustomizationValues(); }
	public void OnNoteColorAFocus() { OnNoteColorAChanged(noteColorA.Text); }
	public void OnNoteColorBChanged(string value) { Settings.NoteColorB = value; Settings.UpdateSettings(); RefreshCustomizationValues(); }
	public void OnNoteColorBFocus() { OnNoteColorBChanged(noteColorB.Text); }
	public void OnCursorScaleChanged(float value) { Settings.CursorScale = value; Settings.UpdateSettings(); }
	public void OnCursorColorChanged(string value) { Settings.CursorColor = value; Settings.UpdateSettings(); RefreshCustomizationValues(); }
	public void OnCursorColorFocus() { OnCursorColorChanged(cursorColor.Text); }

	public void OnCursorPresetChanged(int index)
	{
		if (index <= 0) Settings.CursorPath = "";
		else
		{
			var files = Settings.GetCursorFiles();
			if (index - 1 < files.Length) Settings.CursorPath = files[index - 1];
		}
		Settings.UpdateSettings();
	}

	public void OnColorPresetChanged(int index)
	{
		if (index <= 0) return;
		var files = Settings.GetColorPresetFiles();
		if (index - 1 < files.Length && Settings.ApplyColorPreset(files[index - 1])) RefreshCustomizationValues();
	}

	public void OnSettingsPresetChanged(int index)
	{
		if (index <= 0) return;
		var names = Settings.GetPresetNames();
		if (index - 1 < names.Length && Settings.LoadPreset(names[index - 1])) RefreshCustomizationValues();
	}

	public void OnSavePreset()
	{
		var name = presetName.Text.Trim();
		if (Settings.SavePreset(name))
		{
			RefreshCustomizationLists();
			presetName.Text = "";
		}
	}

	public void OnOpenPresetFolders()
	{
		OS.ShellOpen(OS.GetUserDataDir());
	}

	public void OpenAccount()
	{
		GetNode<TabContainer>("Content").CurrentTab = 3;
		SetActive(true);
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
