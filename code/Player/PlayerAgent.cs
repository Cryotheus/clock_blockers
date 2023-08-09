#nullable enable

using ClockBlockers.AI;
using ClockBlockers.Spectator;
using ClockBlockers.Timeline;
using Sandbox;
using System;
using System.ComponentModel;
using System.Linq;

namespace ClockBlockers;

/// <summary>
/// How is an agent being controlled?
/// </summary>
public enum AgentControlMethod
{
	Player,
	AI,
	Playback
}

partial class PlayerAgent : AnimatedEntity
{
	/// <summary>
	/// Called when the entity is first created 
	/// </summary>
	public override void Spawn()
	{
		Event.Run( "Player.PreSpawn", this );
		base.Spawn();
		Velocity = Vector3.Zero;
		Components.RemoveAll();
		LifeState = LifeState.Alive;
		Health = 100;

		SetModel( "models/citizen/citizen.vmdl" );
		Components.Add( new WalkController() );
		Components.Add( new FirstPersonCamera() );
		Components.Add( new AmmoStorageComponent() );
		Components.Add( new InventoryComponent() );
		Components.Add( new CitizenAnimationComponent() );
		Components.Add( new UseComponent() );
		Components.Add( new FallDamageComponent() );
		Components.Add( new UnstuckComponent() );
		Ammo.ClearAmmo();
		CreateHull();
		Tags.Add( "player" );
		EnableAllCollisions = true;
		EnableDrawing = true;
		EnableHideInFirstPerson = true;
		EnableShadowInFirstPerson = true;
		EnableTouch = true;
		EnableLagCompensation = true;
		Predictable = true;
		EnableHitboxes = true;

		EnableLagCompensation = true;

		MoveToSpawnpoint();
		Event.Run( "Player.PostSpawn", this );
	}

	/// <summary>
	/// Respawn this player.
	/// </summary>
	/// 
	public virtual void Respawn()
	{
		Event.Run( "Player.PreRespawn", this );
		Spawn();
		Event.Run( "Player.PostRespawn", this );
	}

	public virtual void MoveToSpawnpoint()
	{
		// Get all of the spawnpoints
		var spawnpoints = Entity.All.OfType<SpawnPoint>();

		// chose a random one
		var randomSpawnPoint = spawnpoints.OrderBy( x => Guid.NewGuid() ).FirstOrDefault();

		// if it exists, place the pawn there
		if ( randomSpawnPoint != null )
		{
			var tx = randomSpawnPoint.Transform;
			tx.Position = tx.Position + Vector3.Up * 50.0f; // raise it up
			Transform = tx;
		}
	}

	public virtual void OnStartRound(Round round)
	{

	}

	public virtual void OnEndRound(Round round)
	{
		if ( Inventory != null ) Inventory.ActiveChild = null;
		FinalizeAnimations();
	}

	// An example BuildInput method within a player's Pawn class.
	[ClientInput] public Vector3 InputDirection { get; set; }
	[ClientInput] public Angles ViewAngles { get; set; }

	public MovementComponent MovementController => Components.Get<MovementComponent>();
	public CameraComponent CameraController => Components.Get<CameraComponent>();
	public AnimationComponent AnimationController => Components.Get<AnimationComponent>();
	public InventoryComponent Inventory => Components.Get<InventoryComponent>();
	public AmmoStorageComponent Ammo => Components.Get<AmmoStorageComponent>();
	public UseComponent UseComponent => Components.Get<UseComponent>();
	public UnstuckComponent UnstuckController => Components.Get<UnstuckComponent>();
	public AIComponent? AIController => Components.Get<AIComponent>();

	public Entity? ActiveWeapon => Inventory?.ActiveChild;

	/// <summary>
	/// Position a player should be looking from in world space.
	/// </summary>
	[Browsable( false )]
	public Vector3 EyePosition
	{
		get => Transform.PointToWorld( EyeLocalPosition );
		set => EyeLocalPosition = Transform.PointToLocal( value );
	}

	/// <summary>
	/// Position a player should be looking from in local to the entity coordinates.
	/// </summary>
	[Net, Predicted, Browsable( false )]
	public Vector3 EyeLocalPosition { get; set; }

	/// <summary>
	/// Rotation of the entity's "eyes", i.e. rotation for the camera when this entity is used as the view entity.
	/// </summary>
	[Browsable( false )]
	public Rotation EyeRotation
	{
		get => Transform.RotationToWorld( EyeLocalRotation );
		set => EyeLocalRotation = Transform.RotationToLocal( value );
	}

	/// <summary>
	/// Rotation of the entity's "eyes", i.e. rotation for the camera when this entity is used as the view entity. In local to the entity coordinates.
	/// </summary>
	[Net, Predicted, Browsable( false )]
	public Rotation EyeLocalRotation { get; set; }

	public BBox Hull
	{
		get => new
		(
			new Vector3( -16, -16, 0 ),
			new Vector3( 16, 16, 72 )
		);
	}

	public override Ray AimRay => new Ray( EyePosition, EyeRotation.Forward );
	/// <summary>
	/// Create a physics hull for this player. The hull stops physics objects and players passing through
	/// the player. It's basically a big solid box. It also what hits triggers and stuff.
	/// The player doesn't use this hull for its movement size.
	/// </summary>
	public virtual void CreateHull()
	{
		SetupPhysicsFromAABB( PhysicsMotionType.Keyframed, Hull.Mins, Hull.Maxs );

		//var capsule = new Capsule( new Vector3( 0, 0, 16 ), new Vector3( 0, 0, 72 - 16 ), 32 );
		//var phys = SetupPhysicsFromCapsule( PhysicsMotionType.Keyframed, capsule );


		//	phys.GetBody(0).RemoveShadowController();

		// TODO - investigate this? if we don't set movetype then the lerp is too much. Can we control lerp amount?
		// if so we should expose that instead, that would be awesome.
		EnableHitboxes = true;
	}
	DamageInfo LastDamage;
	public override void TakeDamage( DamageInfo info )
	{
		if ( Game.IsClient ) return;
		Event.Run( "Player.PreTakeDamage", info, this );
		LastDamage = info;
		LastAttacker = info.Attacker;
		LastAttackerWeapon = info.Weapon;
		if ( Health > 0f && LifeState == LifeState.Alive )
		{
			Health -= info.Damage;
			if ( Health <= 0f )
			{
				Health = 0f;
				OnKilled();
			}
		}
		Event.Run( "Player.PostTakeDamage", info, this );
	}
	public override void OnKilled()
	{
		if ( Game.IsClient ) return;

		Event.Run( "Player.PreOnKilled", this );
		LifeState = LifeState.Dead;
		BecomeRagdoll( LastDamage );

		Inventory?.DropItem( Inventory?.ActiveChild );
		Inventory?.Items.Clear();
		EnableDrawing = false;

		if (HasClient)
		{
			SpectatorPawn specPawn = SpectatorPawn.Create();
			Client.Pawn = specPawn;
			specPawn.Position = EyePosition;
			specPawn.ViewAngles = ViewAngles;
		}
		if (IsRecording)
		{
			TimelineCapture?.Event( new DeathEvent(), true );
		}

		FinalizeAnimations();

		Event.Run( "Player.PostOnKilled", this );
		this.Delete();
	}

	//---------------------------------------------// 

	public bool HasClient => Client != null && Client.Pawn == this;

	/// <summary>
	/// The current control method of this agent.
	/// </summary>
	[Net]
	public AgentControlMethod ControlMethod { get; private set; } = AgentControlMethod.Player;

	public void SetControlMethod( AgentControlMethod controlMethod )
	{
		if ( !Game.IsServer ) return;

		var prevControlMethod = ControlMethod;
		if ( controlMethod == prevControlMethod ) return;

		ControlMethod = controlMethod;

		// Do we actually want to remove this or just disable it?
		if (controlMethod != AgentControlMethod.AI)
		{
			Components.RemoveAny<AIComponent>();
		}

		if ( controlMethod == AgentControlMethod.Player )
		{
			Components.Create<WalkController>();
		}
		else if ( controlMethod == AgentControlMethod.Playback )
		{
			Components.Create<PlaybackMovementController>();
		}
		else
		{
			Components.Create<AIMovementController>();
		}
	}

	/// <summary>
	/// Whether this agent is considered a "free agent"
	/// </summary>
	public bool IsFreeAgent => ControlMethod != AgentControlMethod.Playback;

	/// <summary>
	/// Pawns get a chance to mess with the input. This is called on the client.
	/// </summary>
	public override void BuildInput()
	{
		base.BuildInput();
		// these are to be done in order and before the simulated components
		CameraController?.BuildInput();
		MovementController?.BuildInput();
		AnimationController?.BuildInput();

		foreach ( var i in Components.GetAll<SimulatedComponent>())
		{
			if ( i.Enabled ) i.BuildInput();
		}
	}

	public override void Simulate( IClient cl )
	{
		base.Simulate( cl );
		if ( HasClient )
			Tick( cl );
		
	}

	[GameEvent.Tick.Server]
	protected void TickServer()
	{
		if ( !HasClient )
			Tick( null );
	}

	/// <summary>
	/// Called every tick.
	/// If this agent is controlled by a client, this is called during <c>Simulate</c>.
	/// If not, it's called during <c>GameEvent.Tick.Server</c>.
	/// </summary>
	public virtual void Tick( IClient? cl )
	{
		if ( IsLocalPawn )
		{
			// toggleable third person
			if ( Input.Pressed( "View" ) && Game.IsServer )
			{
				Log.Info( "thirdperson" );
				if ( CameraController is FirstPersonCamera )
				{
					Components.Add( new ThirdPersonCamera() );
				}
				else if ( CameraController is ThirdPersonCamera )
				{
					Components.Add( new FirstPersonCamera() );
				}
			}
			if ( ControlMethod == AgentControlMethod.Player )
			{
				if ( Input.MouseWheel > 0.1 )
				{
					Inventory?.SwitchActiveSlot( 1, true );
				}
				if ( Input.MouseWheel < -0.1 )
				{
					Inventory?.SwitchActiveSlot( -1, true );
				}
			}
		}
		// these are to be done in order and before the simulated components
		// Inputs

		if ( IsFreeAgent )
			UnstuckController?.Simulate( cl );

		MovementController?.Simulate( cl );

		// Time shit in between inputs and outputs.
		// With the playback movement controller, animation can manipulate tags for animation purposes.
		TickTimeShit( cl );

		// Outputs
		CameraController?.Simulate( cl );
		AnimationController?.Simulate( cl );
		foreach ( var i in Components.GetAll<SimulatedComponent>() )
		{
			if ( i.Enabled ) i.Simulate( cl );
		}

		// Ensure all viewmodels are destroyed
		if ( !HasClient && Inventory != null )
		{
			foreach ( var item in Inventory.Items )
			{
				if ( item is Carriable carriable ) carriable.DestroyViewModel();
			}
		}
	}

	/// <summary>
	/// Called every frame on the client
	/// </summary>
	public override void FrameSimulate( IClient cl )
	{
		base.FrameSimulate( cl );
		// these are to be done in order and before the simulated components
		UnstuckController?.FrameSimulate( cl );
		MovementController?.FrameSimulate( cl );
		CameraController?.FrameSimulate( cl );
		AnimationController?.FrameSimulate( cl );
		foreach ( var i in Components.GetAll<SimulatedComponent>() )
		{
			if ( i.Enabled ) i.FrameSimulate( cl );
		}
	}
	TimeSince timeSinceLastFootstep = 0;
	public override void OnAnimEventFootstep( Vector3 position, int foot, float volume )
	{
		if ( LifeState != LifeState.Alive )
			return;

		if ( Game.IsServer )
			return;

		if ( timeSinceLastFootstep < 0.2f )
			return;
		volume *= FootstepVolume();
		var tr = Trace.Ray( position, position + Vector3.Down * 20 ).Radius( 1 ).Ignore( this ).Run();
		if ( !tr.Hit ) return;
		timeSinceLastFootstep = 0;
		tr.Surface.DoFootstep( this, tr, foot, volume * 10 );
	}

	public virtual float FootstepVolume()
	{
		if ( MovementController is WalkController wlk )
		{
			if ( wlk.IsDucking ) return 0.3f;
		}
		return 1;
	}

	public TraceResult ViewTarget { get; protected set; }

	[GameEvent.Tick]
	protected virtual void UpdateViewTarget()
	{
		Trace trace = Trace.Ray( AimRay, 128 )
			.WithAnyTags( "solid", "glass" )
			.Ignore( this );
		ViewTarget = trace.Run();
	}
}
