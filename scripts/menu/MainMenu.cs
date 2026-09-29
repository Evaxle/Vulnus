using Godot;
using System;

public class MainMenu : View
{
	private Label username;
	public override void _Ready()
	{
		base._Ready();
		MenuHandler menu = GetParent<Control>().GetParent<MenuHandler>();
		var sidebar = GetNode<Control>("Sidebar");
		var buttons = sidebar.GetNode<Control>("Buttons");
		var signInBtn = buttons.GetNode<Button>("SignIn");
		signInBtn.Connect("pressed", this, nameof(OpenRhythiansAccount));
		var singleBtn = buttons.GetNode<Button>("Singleplayer");
		singleBtn.Connect("pressed", menu, nameof(MenuHandler.GoTo), new Godot.Collections.Array(1));
		var downloadsBtn = buttons.GetNode<Button>("Downloads");
		downloadsBtn.Connect("pressed", menu, nameof(MenuHandler.GoTo), new Godot.Collections.Array(3));
		var optionsBtn = buttons.GetNode<Button>("Options");
		optionsBtn.Connect("pressed", Global.Instance.Overlays["Options"], nameof(Options.SetActive), new Godot.Collections.Array(true));
		var exitBtn = buttons.GetNode<Button>("Quit");
		exitBtn.Connect("pressed", this, nameof(Quit));
		username = menu.GetNode<Label>("Topbar/Profile/Username");
		RhythiansApi.AccountChanged += UpdateAccountLabel;
		UpdateAccountLabel();
	}
	public override void _ExitTree()
	{
		RhythiansApi.AccountChanged -= UpdateAccountLabel;
		base._ExitTree();
	}

	private void UpdateAccountLabel()
	{
		if (username != null)
			username.Text = RhythiansApi.IsAuthenticated ? RhythiansApi.Username ?? "Rhythians user" : "Not logged in";
	}

	public void OpenRhythiansAccount()
	{
		((Options)Global.Instance.Overlays["Options"]).OpenAccount();
	}
	public void Quit()
	{
		GetTree().Quit(0);
	}
}
