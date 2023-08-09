#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using ClockBlockers.Timeline;
using ClockBlockers.Weapon;
using Sandbox;

namespace ClockBlockers.Anim;

/// <summary>
/// A discrete action performed in an animation, such as jumping or shooting.
/// </summary>
public interface IAction
{
	/// <summary>
	/// The action ID to use if this action is continuous.
	/// </summary>
	public string? ActionID => null;

	/// <summary>
	/// Run this action.
	/// </summary>
	/// <param name="pawn">Pawn to use.</param>
	/// <returns>If this action should act as a continuous action.</returns>
	public bool Run( PlayerAgent pawn );

	/// <summary>
	/// Stop this action. Called before any replacement action is started.
	/// </summary>
	/// <param name="pawn">The pawn stopping the action</param>
	public void Stop( PlayerAgent pawn ) { }

}

/// <summary>
/// Stops any action with the target ID.
/// Implementation is in the anim player rather than the action.
/// </summary>
public struct StopAction : IAction
{
	public string TargetID { get; set; }

	public StopAction( string targetID )
	{
		TargetID = targetID;
	}

	public bool Run( PlayerAgent pawn ) { return false; }
}

public struct JumpAction : IAction
{
	public bool Run( PlayerAgent pawn )
	{
		pawn.MovementController.AddEvent( "jump" );
		return false;
	}

}

public struct UseAction : IAction
{
	public static readonly string ID = "use";

	public string ActionID => ID;
	public string TargetID { get; set; } = "";
	public bool Continuous { get; set; }
	public UseAction() { }
	public UseAction( Entity target )
	{
		TargetID = target.GetPersistentIDOrThrow( true );
	}

	public bool Run( PlayerAgent pawn )
	{
		Entity? target = PersistentEntities.GetEntity( TargetID );
		if ( target is not IUse )
		{
			Log.Warning( $"Use target '{TargetID}' not usable!" );
			return false;
		}
		pawn.UseComponent.StartUsing( target );
		return Continuous;
	}

	public void Stop( PlayerAgent pawn )
	{
		pawn.UseComponent.StopUsing();
	}
}

public struct DropWeaponAction : IAction
{
	public Vector3 Velocity { get; set; }
	public string? WeaponID { get; set; }

	public bool Run( PlayerAgent pawn )
	{
		var entity = WeaponID != null ? PersistentEntities.GetEntity( WeaponID ) : null;
		if ( entity == null )
			entity = pawn.Inventory.ActiveChild;

		if ( entity is not Carriable carriable )
			return false;

		pawn.Inventory.DropItem( carriable, Velocity );
		return false;
	}
}

public struct PickupWeaponAction : IAction
{
	public string WeaponID { get; set; }

	public PickupWeaponAction( string weaponID )
	{
		WeaponID = weaponID;
	}

	public PickupWeaponAction( Carriable weapon )
	{
		WeaponID = weapon.GetPersistentIDOrCreate();
	}

	public bool Run( PlayerAgent pawn )
	{
		var entity = PersistentEntities.GetEntity( WeaponID );
		if ( entity is not Carriable carriable ) return false;
		pawn.Inventory.PickupItem( carriable );
		return false;
	}
}


public struct ShootEffectsAction : IAction
{
	public static readonly string ID = "ShootEffects";

	public string ActionID => ID;

	public bool IsContinuous { get; set; }

	public ShootEffectsAction( bool isContinuous )
	{
		IsContinuous = isContinuous;
	}

	public bool Run( PlayerAgent pawn )
	{
		if ( pawn.ActiveWeapon is BaseFirearm firearm )
		{
			firearm.DoShootEffects();
		}

		return IsContinuous;
	}

	public void Stop( PlayerAgent pawn )
	{
		if ( pawn.ActiveWeapon is BaseFirearm firearm )
		{
			firearm.StopShootEffects();
		}
	}
}

//public struct PickUpWeaponAction : IAction
//{
//	public string WeaponID { get; set; }

//	public PickUpWeaponAction(string id)
//	{
//		WeaponID = id;
//	}

//	public PickUpWeaponAction(LegacyWeapon weapon)
//	{
//		WeaponID = weapon.GetPersistentIDOrThrow( true );
//	}

//	public void Run( AgentPawn pawn )
//	{
//		var weapon = PersistentEntities.GetEntity<LegacyWeapon>( WeaponID );
//		if ( weapon == null || weapon.IsHeld ) return;

//		pawn.PickUpWeapon( weapon );
//	}
//}
