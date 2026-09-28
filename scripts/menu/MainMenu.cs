using Godot;
using System;

public class MainMenu : View
{
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
		var optionsBtn = buttons.GetNode<Button>("Options");
		optionsBtn.Connect("pressed", Global.Instance.Overlays["Options"], nameof(Options.SetActive), new Godot.Collections.Array(true));
		var exitBtn = buttons.GetNode<Button>("Quit");
		exitBtn.Connect("pressed", this, nameof(Quit));
		var username = menu.GetNode<Label>("Topbar/Profile/Username");
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
