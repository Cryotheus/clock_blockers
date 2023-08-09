#nullable enable

using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClockBlockers.Anim;

/// <summary>
/// Responsible for capturing the animation of a pawn.
/// </summary>
public class AnimCapture : EntityComponent<PlayerAgent>, ISingletonComponent
{
	public AnimCapture()
	{
		ShouldTransmit = false;
	}

	private TimeSince _startTime;

	/// <summary>
	/// Whether this anim capture is currently recording.
	/// </summary>
	public bool IsRecording { get; private set; }

	/// <summary>
	/// The animation this capture is recording (or recorded) to.
	/// </summary>
	public Animation? Animation { get; private set; }

	private List<IAction> _cachedActions = new();

	public void Start()
	{
		if ( IsRecording )
		{
			throw new InvalidOperationException( "Animation already is recording or has finished." );
		}

		Animation = new();
		Animation.TickRate = Game.TickRate;
		Animation.Clothing = Entity.Clothing;

		IsRecording = true;
		_startTime = 0;
		Log.Info( $"{this} started capturing animation." );
	}


	public void Tick()
	{
		if ( !IsRecording || Animation == null ) return;
		if ( _startTime < 0 ) _startTime = 0;

		int segmentIndex = (int)MathF.Floor( _startTime );

		// Generally this should act as an if statement, only iterating once.
		while ( segmentIndex >= Animation.Segments.Count )
		{
			Animation.Segments.Add( new AnimSegment() );
		}

		AnimSegment segment = Animation.Segments[segmentIndex];

		int tickIndex = segment.Frames.Count;
		segment.Frames.Add( AnimFrame.Capture( Entity ) );

		if ( Entity.MovementController != null && Entity.MovementController.HasEvent( "jump" ) )
			segment.AddAction( tickIndex, new JumpAction() );

		if ( _cachedActions.Count > 0 ) segment.AddActions( tickIndex, _cachedActions );
		_cachedActions.Clear();
	}

	public Animation Stop()
	{
		if ( Animation == null )
		{
			throw new InvalidOperationException( "Not recording." );
		};

		IsRecording = false;
		return Animation;
	}

	/// <summary>
	/// Add an action to be played on the current frame (must be called BEFORE tick())
	/// </summary>
	/// <param name="action">Action to add</param>
	public void AddAction( IAction action )
	{
		_cachedActions.Add( action );
	}
}
