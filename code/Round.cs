#nullable enable

using ClockBlockers.Anim;
using ClockBlockers.Spectator;
using ClockBlockers.Timeline;
using ClockBlockers.Util;
using ClockBlockers.Weapon;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ClockBlockers;

public partial class Round : EntityComponent<ClockBlockersGame>, ISingletonComponent
{
	public static readonly float ROUND_TIME = 10;

	private LinkedList<PlayerAgent> Pawns = new();

	private TaskCompletionSource<IEnumerable<TimelineBranch>> task = new();

	public Task<IEnumerable<TimelineBranch>>? RoundTask => task.Task;

	[Net]
	public TimeUntil TimeLeft { get; private set; }

	[Net]
	public bool RoundStarted { get; private set; }

	[Net]
	public bool RoundEnded { get; private set; }

	[Net]
	public int RoundID { get; private set; }

	public void Start( int roundID, IEnumerable<TimelineBranch>? existingTimelines = null )
	{
		Game.AssertServer();

		if ( RoundStarted )
		{
			throw new InvalidOperationException( "Round has already started." );
		}
		Game.ResetMap( new Entity[] { } );

		TimeLeft = ROUND_TIME;
		RoundStarted = true;
		RoundID = roundID;

		int remnants = 0;
		if ( existingTimelines != null ) foreach ( var t in existingTimelines )
			{
				SpawnRemnant( t );
				remnants++;
			}

		int clients = 0;
		foreach ( var client in Game.Clients )
		{
			var oldPawn = client.Pawn;
			SpawnPlayerPawn( client, weapon: WeaponTypes.Get( WeaponTypes.PISTOL ) );
			oldPawn?.Delete();
			clients++;
		}

		task = new();
		Log.Info( $"Started round with {clients} players and {remnants} remnants." );
	}

	public void Tick()
	{
		if ( RoundStarted && TimeLeft <= 0 ) EndRound();
	}

	protected LinkedList<TimelineBranch> finalBranches = new();

	public IEnumerable<TimelineBranch> EndRound()
	{
		foreach ( PlayerAgent pawn in Pawns )
		{
			if ( pawn.Client != null )
				pawn.Client.Pawn = SpectatorPawn.Create();


			pawn.OnEndRound(this);
			var t = pawn.ActiveTimeline;
			if ( t != null ) finalBranches.AddLast( t );
		}

		foreach ( var pawn in Pawns ) pawn.Delete();

		task.SetResult( finalBranches );
		return finalBranches;
	}

	protected void SpawnPlayerPawn( IClient cl, WeaponTypes.WeaponFactory? weapon = null )
	{
		var oldPawn = cl.Pawn;

		var pawn = new PlayerAgent();
		cl.Pawn = pawn;

		// chose a random one
		var randomSpawnPoint = RandomSpawnPoint( Sandbox.Entity.All.OfType<SpawnPoint>() );

		// if it exists, place the pawn there
		if ( randomSpawnPoint != null )
		{
			var tx = randomSpawnPoint.Transform;
			tx.Position = tx.Position + Vector3.Up * 50.0f; // raise it up
			pawn.Transform = tx;
		}

		//pawn.DressFromClient( cl );
		//pawn.PostSpawn();

		var id = $"{cl.SteamId}.round{RoundID}";
		pawn.SetPersistentID( id );

		// This will start the timeline capture.
		pawn.InitTimeShit( AgentControlMethod.Player );

		if ( weapon != null )
		{
			var spawner = new WeaponTypes.Spawner()
			{
				Factory = weapon,
				PersistentID = $"{id}.weapon"
			};

			pawn.TimelineCapture?.SetWeaponSpawn( spawner );
			pawn.Inventory.AddActiveChild( spawner.Spawn() );
			
		}

		Pawns.AddLast( pawn );

		if ( oldPawn != null ) oldPawn.Delete();
	}

	protected void SpawnRemnant( TimelineBranch timeline )
	{
		var player = new PlayerAgent();
		if ( timeline.PersistentID != null )
		{
			player.SetPersistentID( timeline.PersistentID );
		}

		//player.SetControlMethod( AgentControlMethod.Playback );
		//player.Inventory.AddItem( new Pistol() );
		//player.CreateAnimPlayer().Play( timeline.Animation );
		//var pawn = new Player();


		//pawn.Clothing = timeline.Animation.Clothing;
		player.InitTimeShit( AgentControlMethod.Playback, timeline );

		Pawns.AddLast( player );
	}

	/// <summary>
	/// Choose a random spawn point, attempting to find one that's not occupied.
	/// </summary>
	/// <param name="spawnpoints">An enumerable of all the spawnpoints in the map.</param>
	/// <returns>The spawn point, or <c>null</c> if the map has no spawn points.</returns>
	public static SpawnPoint? RandomSpawnPoint( IEnumerable<SpawnPoint> spawnpoints )
	{
		if ( !spawnpoints.Any() ) return null;

		var filtered = spawnpoints.Where( sp => !sp.IsOccupied() );
		if ( filtered.Any() )
		{
			return filtered.RandomElement();
		}
		else
		{
			return spawnpoints.RandomElement();
		}
	}

	/// <summary>
	/// Because players will be deleted when killed, we need to save their animation data
	/// now so its not deleted as well.
	/// </summary>
	[Event( "Player.PostOnKilled" )]
	protected void OnPlayerKilled(PlayerAgent player)
	{
		var t = player.ActiveTimeline;
		if ( t != null ) finalBranches.AddLast( t );
	}
}
