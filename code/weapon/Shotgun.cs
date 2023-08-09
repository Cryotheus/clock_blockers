using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClockBlockers.Weapon;

public partial class Shotgun : BaseFirearm
{
	public override string ViewModelPath => "weapons/rust_pumpshotgun/v_rust_pumpshotgun";
	public override string WorldModelPath => "weapons/rust_pumpshotgun/rust_pumpshotgun";

	public override CitizenAnimationHelper.HoldTypes HoldType => CitizenAnimationHelper.HoldTypes.Shotgun;

	private static readonly int SHOT_COUNT = 9;
	public static readonly float FIRE_INTERVAL = 1f;

	protected TimeSince LastFire { get; set; }

	public virtual void PrimaryFire()
	{

		using (LagCompensation())
		{
			BulletInfo[] bullets = new BulletInfo[SHOT_COUNT];
			for ( int i = 0; i < SHOT_COUNT; i++ )
			{
				bullets[i] = BulletHelper.FromWeapon( this, 10f, spread: .25f );
			}
			FireBullets( bullets );
		}

		DoShootEffects();
		LastFire = 0;
	}

	public override void Simulate( IClient cl )
	{
		base.Simulate( cl );
		if ( Input.Pressed( "attack1" ) && LastFire > FIRE_INTERVAL )
		{
			PrimaryFire();
		}
	}

	public override void DoShootEffects()
	{
		base.DoShootEffects();

		if ( !Prediction.FirstTime ) return;
		Pawn?.PlaySound( "rust_pistol.shoot" );

		Particles.Create( "particles/pistol_muzzleflash.vpcf", EffectEntity, "muzzle" );
		Pawn?.SetAnimParameter( "b_attack", true );
		ViewModelEntity?.SetAnimParameter( "fire", true );
	}
}
