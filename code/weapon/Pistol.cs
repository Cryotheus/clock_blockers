using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClockBlockers.Weapon;

public partial class Pistol : BaseFirearm
{
	public override string WorldModelPath => "weapons/rust_pistol/rust_pistol.vmdl";
	public override string ViewModelPath => "weapons/rust_pistol/v_rust_pistol.vmdl";

	public void PrimaryFire()
	{
		using (LagCompensation())
		{
			BulletInfo bullet = BulletHelper.FromWeapon( this, 20f );
			FireBullet( bullet );
		}
		DoShootEffects();
	}

	public override void Simulate( IClient cl )
	{
		base.Simulate( cl );
		if ( Input.Pressed( "attack1" ) )
		{
			PrimaryFire();
		}
	}

	public override void DoShootEffects()
	{
		base.DoShootEffects();

		if ( !Prediction.FirstTime ) return;
		Pawn?.PlaySound( "rust_pumpshotgun.shoot" );

		Particles.Create( "particles/pistol_muzzleflash.vpcf", EffectEntity, "muzzle" );
		Pawn?.SetAnimParameter( "b_attack", true );
		ViewModelEntity?.SetAnimParameter( "fire", true );
	}
}
