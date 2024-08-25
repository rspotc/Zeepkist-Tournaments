using System;

public class TopOutPlayer
{
	public TopOutPlayer(string uname) { username = uname; }

	public string username;
	public int points = 0;
	public int winnerPosition = -1;
	public bool finalist = false;
}
