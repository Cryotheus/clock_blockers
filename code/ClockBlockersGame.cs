#nullable enable

using ClockBlockers.Spectator;
using ClockBlockers.Timeline;
using ClockBlockers.UI;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace ClockBlockers;

/// <summary>
/// This is your game class. This is an entity that is created serverside when
/// the game starts, and is replicated to the client. 
/// 
/// You can use this to create things like HUDs and declare which player class
/// to use for spawned players.
/// </summary>
public partial class ClockBlockersGame : Sandbox.GameManager
{
	[BindComponent] public Round? Round { get; }

	public Dictionary<string, TimelineBranch> Timelines { get; } = new();

	[Net]
	public int RoundID { get; protected set; }

	/// <summary>
	/// Called when the game is created (on both the server and client)
	/// </summary>
	public ClockBlockersGame()
	{
		if ( Game.IsClient )
		{
			Game.RootPanel = new HUDRootPanel();
		}
	}

	[GameEvent.Tick.Server]
	public void Tick()
	{
		Round?.Tick();
	}

	/// <summary>
	/// A client has joined the server. Make them a pawn to play with
	/// </summary>
	public override void ClientJoined( IClient client )
	{
		base.ClientJoined( client );

		// Create a pawn for this client to play with
		var specPawn = new SpectatorPawn();
		client.Pawn = specPawn;

		client.Pawn = SpectatorPawn.Create();
	}

	public async void DoRound()
	{
		if (Round != null)
			throw new InvalidOperationException( "There is already a round running." );

		Components.Create<Round>();
		if (Round == null)
			throw new InvalidOperationException( "Failed to create round." );

		RoundID++;
		Log.Info( $"Round {RoundID} starting. Duration: {Round.ROUND_TIME} seconds" );

		Round.Start( RoundID, Timelines.Values );

		if (Round.RoundTask == null)
		{
			Components.Remove( Round );
			throw new InvalidOperationException( "Failed to start round." );
		}

			
		var newTimelines = await Round.RoundTask;

		foreach (var timeline in newTimelines)
		{
			var id = timeline.PersistentID;
			if (id == null)
			{
				Log.Warning( "Timeline did not have a persistent ID. Assigning a random one." );
				id = PersistentEntities.RandomID();
			}

			Timelines.TryAdd( id, timeline );
		}

		Log.Info( $"Round {RoundID} completed." );
		Components.Remove( Round );
	}

}

