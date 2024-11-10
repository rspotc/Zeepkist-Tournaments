public class TopOutPlayer
{
	public TopOutPlayer(string uname, string fullUname) { username = uname; fullUsername = fullUname; }

	public string username;
	public string fullUsername;
	public int points = 0;
	public int winnerPosition = -1;
	public bool finalist = false;
	public bool nuisance = false;
	public float currentTime = 0;
}
