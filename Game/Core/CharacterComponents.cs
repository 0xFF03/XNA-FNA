namespace MyGame.Game.Core;

public struct CharacterClass
{
	public int Id;
}

public struct GroundState
{
	public bool IsGrounded;
	public float CoyoteTimer;
}

public struct LocalPlayerTag { }
public struct RemotePlayerTag { }
public struct MatchEntityTag { }
