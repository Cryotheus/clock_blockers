﻿#nullable enable

using ClockBlockers.Anim;
using ClockBlockers.Spectator;
using ClockBlockers.Timeline;
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
	public static readonly float ROUND_TIME = 15f;

	private LinkedList<Pawn> pawns = new();

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
		Game.ResetMap( new Sandbox.Entity[] { } );

		TimeLeft = ROUND_TIME;
		RoundStarted = true;
		RoundID = roundID;

		int clients = 0;
		foreach ( var client in Game.Clients )
		{
			var oldPawn = client.Pawn;
			SpawnPlayerPawn( client );
			oldPawn?.Delete();
			clients++;
		}

		int remnants = 0;
		if ( existingTimelines != null ) foreach ( var t in existingTimelines )
			{
				SpawnRemnant( t );
				remnants++;
			}

		task = new();
		Log.Info( $"Started round with {clients} players and {remnants} remnants." );
	}

	public void Tick()
	{
		if ( RoundStarted && TimeLeft <= 0 ) EndRound();
	}

	public IEnumerable<TimelineBranch> EndRound()
	{
		LinkedList<TimelineBranch> finalBranches = new();

		foreach ( Pawn pawn in pawns )
		{
			if ( pawn.Client != null )
				pawn.Client.Pawn = SpectatorPawn.SpawnEntity();


			var t = pawn.ActiveTimeline;
			if ( t != null ) finalBranches.AddLast( t );
		}

		foreach ( Pawn pawn in pawns ) pawn.Delete();

		task.SetResult( finalBranches );
		return finalBranches;
	}

	protected void SpawnPlayerPawn( IClient cl )
	{
		var oldPawn = cl.Pawn;

		var pawn = new Pawn();
		cl.Pawn = pawn;

		// chose a random one
		var spawnpoints = Sandbox.Entity.All.OfType<SpawnPoint>();
		var randomSpawnPoint = spawnpoints.OrderBy( x => Guid.NewGuid() ).FirstOrDefault();

		// if it exists, place the pawn there
		if ( randomSpawnPoint != null )
		{
			var tx = randomSpawnPoint.Transform;
			tx.Position = tx.Position + Vector3.Up * 50.0f; // raise it up
			pawn.Transform = tx;
		}

		pawn.DressFromClient( cl );
		pawn.PostSpawn();

		pawn.SetPersistentID( $"{cl.SteamId}.round{RoundID}" );
		pawn.InitTimeTravel( Pawn.PawnControlMethod.Player );

		pawns.AddLast( pawn );

		if ( oldPawn != null ) oldPawn.Delete();
	}

	protected void SpawnRemnant( TimelineBranch timeline )
	{
		var pawn = new Pawn();
		pawn.PostSpawn();

		if ( timeline.PersistentID != null )
		{
			pawn.SetPersistentID( timeline.PersistentID );
		}

		pawn.Clothing = timeline.Animation.Clothing;
		pawn.InitTimeTravel( Pawn.PawnControlMethod.Animated, timeline );

		pawns.AddLast( pawn );
	}
}