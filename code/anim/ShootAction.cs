#nullable enable

using ClockBlockers.Timeline;
using ClockBlockers.Weapon;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClockBlockers.Anim;

/// THIS CLASS HAS BEEN TEMPORARILY MODIFIED TO WORKAROUND AN SBOX BUG: https://github.com/sboxgame/issues/issues/3745
/// Reset to commit fee361fd59eac4e8e4ca7eec1b3960de5781f6d5 once fixed.
public struct ShootAction : IAction
{
	public BulletInfo Bullet { get; init; }

	/// <summary>
	/// The lag-compensated positions of all relevent entities.
	/// </summary>
	public Dictionary<string, EntityTraceState> EntityPositions { get; set; } = new();

	/// <summary>
	/// A sorted list of all recorded trace results.
	/// </summary>
	public List<SimpleTraceResult> TraceResults { get; set; } = new();

	public ShootAction( BulletInfo bullet )
	{
		Bullet = bullet;
	}

	public bool Run( PlayerAgent pawn )
	{
		if ( pawn.ActiveWeapon is not BaseFirearm firearm ) return false;

		//// Temporarily restore all entities to recorded positions
		//Dictionary<ModelEntity, EntityTraceState> originalPositions = new();
		//foreach ( var entry in EntityPositions )
		//{
		//	var target = PersistentEntities.GetEntity<ModelEntity>( entry.Key );
		//	if ( target != null )
		//	{
		//		Log.Info( $"Original Position: {target.Position}; Lag compensated: {entry.Value}" );
		//		originalPositions.Add( target, new EntityTraceState().CopyFrom( target ) );
		//		//originalPositions.Add( target, target.Position );
		//		entry.Value.ApplyTo( target );

		//	}
		//}

		// Because we can't temporarily update entity positions to compensate for prior lag, we instead
		// snake the trace around to the recorded hit positions using "trace segments"
		if ( !TraceResults.Any() )
		{
			firearm.FireBullet( Bullet, true );
		}
		else
		{
			var startPos = Bullet.Ray.Position;
			Entity ignore = pawn;
			foreach ( var hit in TraceResults )
			{
				var ent = PersistentEntities.GetEntity( hit.EntityID );
				if ( ent == null ) continue;

				Log.Info( "Remnant fired at: " + hit.EntityID );
				var endPos = ent.Transform.PointToWorld( hit.HitPosition );
				var results = firearm.DoBulletTrace( startPos, endPos, ignore );

				if ( DoDamage( firearm, results, Bullet, ent ) )
				{
					startPos = endPos;
					ignore = ent;
				}
				else
				{
					break;
				}
			}
		}


		//foreach ( var entry in originalPositions )
		//{
		//	entry.Value.ApplyTo( entry.Key );
		//}

		return false;
	}

	/// <summary>
	/// Attemt to recreate trace damage from trace segments
	/// </summary>
	/// <param name="firearm">The firearm to use</param>
	/// <param name="results">The results from this trace segment.</param>
	/// <param name="bullet">The bullet fired</param>
	/// <param name="targetEnt">The original entity that was hit.</param>
	/// <returns>Was the target entity still hit?</returns>
	private bool DoDamage(BaseFirearm firearm, IEnumerable<TraceResult> results, BulletInfo bullet, Entity? targetEnt)
	{
		foreach ( var tr in results )
		{
			if ( !tr.Hit ) continue;

			firearm.DealDamage( tr, bullet );
			if ( tr.Entity == targetEnt ) return true;
		}
		return false;
	}

	public class Builder
	{
		public List<SimpleTraceResult> TraceResults { get; init; } = new();

		public void AddHitResult(TraceResult tr)
		{
			if ( tr.Entity is not ModelEntity ) return;
			if ( tr.Entity.GetPersistentID() != null )
				TraceResults.Add( SimpleTraceResult.Create( tr ) );
		}

		public ShootAction Build(BulletInfo bullet)
		{
			return new ShootAction( bullet )
			{
				TraceResults = TraceResults.OrderBy( x => x.HitPosition.DistanceSquared( bullet.Ray.Position ) ).ToList()
			};
		}
	}
}

/// <summary>
/// Simple representation of a trace result hitting an entity.
/// Used as a workaround for https://github.com/sboxgame/issues/issues/3745
/// All fields are local to the hit entity.
/// </summary>
public struct SimpleTraceResult
{

	public string EntityID { get; set; }
	public Vector3 HitPosition { get; set; }
	public Vector3 Normal { get; set; }
	public Vector3 Direction { get; set; }
	public float Distance { get; set; }

	public EntityTraceState TraceState { get; set; }

	public static SimpleTraceResult Create( TraceResult traceResult )
	{
		var entity = traceResult.Entity;
		if ( entity is not ModelEntity modelEntity )
			throw new ArgumentException( "Trace result must have hit a model entity.", nameof( traceResult ) );

		return new SimpleTraceResult
		{
			EntityID = entity.GetPersistentIDOrCreate(),
			HitPosition = entity.Transform.PointToLocal( traceResult.HitPosition ),
			Normal = entity.Transform.NormalToLocal( traceResult.Normal ),
			Direction = entity.Transform.NormalToLocal( traceResult.Direction ),
			Distance = traceResult.Distance,
			TraceState = new EntityTraceState().CopyFrom( modelEntity )
		};
	}
}

public struct EntityTraceState
{
	public Vector3 Position { get; set; }
	public Rotation Rotation { get; set; }

	public Transform[] BoneTransforms { get; set; } = new Transform[0];

	public EntityTraceState()
	{ }

	public EntityTraceState CopyFrom( ModelEntity entity )
	{
		Position = entity.Position;
		Rotation = entity.Rotation;
		BoneTransforms = new Transform[entity.BoneCount];

		for ( int i = 0; i < BoneTransforms.Length; i++ )
		{
			BoneTransforms[i] = entity.GetBoneTransform( i );
		}
		return this;
	}

	public void ApplyTo( ModelEntity entity )
	{
		entity.Position = Position;
		entity.Rotation = Rotation;

		for ( int i = 0; i < BoneTransforms.Length; i++ )
		{
			entity.SetBoneTransform( i, BoneTransforms[i] );
		}

	}
}
