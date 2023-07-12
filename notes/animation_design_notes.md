# Design Goals

- Efficiently store player gameplay data in a format that doesn't eat memory space and *possibly* can be serialized to disk.

- Correction for dropped ticks / lag

- Easily scrubbable for use in the gameplay inspector
  
  - Random access of the frame at a given timestamp

- Store discrete "events" so they can be located without scanning through frames

# Notes

- Although metadata at the beginning may store the tickrate, it cannot be assumed that it will play back properly at a different tickrate than that at which it was recorded.

- While player position is recorded and may be easily scrubbed through, the state of other entities on the map may be determined procedurally (physics, etc) and therefore may be hard to scrub through.
  
  - Using the demo system is easy but hard to control
  
  - Potentially a separate "map" recording with the position / state of every model (without any gameplay data) which can be easily played back. This would also fix issues with rewinding.

- Likely want to animate velocity as well as position (for prediction)

- Ammo count should not be recorded as this could lead to unwanted unlinks

- Store clothing container with animation because it's serializable 🙂

- How to keep track of non-player entities that may impact animation for unlinking purposes? (grenades, etc.)
  
  - Entity component storing some kind of ID, which gets added to the animation?

- Do we keep track of remnant health or just when they're killed?
  
  - We need to in order to allow free agents to kill them. The question is whether remnants damaging remnants should be tracked.

- When animations are being played in a new timeline context (new round), create a new animation object and copy original data to it. Interactions with new entities are added here.

## Events

- Putting an event table at the beginning of the file makes them easily accessible globally, but is less efficient on a per-frame basis, as each event would have to be checked on each frame.

- Events cannot be *purely* for unlinking purposes as they might impact the animation playback (pressing buttons, etc)

- Do we store the frame they land on or the timestamp? Storing the frame makes it easier during playback, but it makes it harder to determine *when* they occur looking at the file overall.
  
  - Probably timestamp

- On a given frame, how to determine *which* timestamps count as part of that frame?

- Some events impact frames going forward (changing weapon, etc). How to read while scrubbing?

**Solution:** Two types of events:

- *Events*
  
  - Indexed at the beginning of file with timecode
  
  - Not stored with frame
  
  - Must have a corresponding *action* in order to be seen during playback
  
  - Action failed: unlink
  
  - *ex: player killed, grenade thrown, button pressed, etc*

- *Actions*
  
  - Stored as part of individual frames
  
  - Represent discrete animation data (rather than continuous such as position)
  
  - Only used in animation playback
  
  - Not indexed
  
  - *ex: jump, shoot, button press (animation)*

## Damage Tracking & Unlinks

Damage & health pose a unique challenge when it comes to handling remnants. While a remnant's health must be tracked so it can interact with free agents, its time of death also needs to remain predictable (unless interfered with).

- Store which entities were damaged by how much in `Shoot` action. That way, even if the weapon itself acts non-deterministically (random bullet spread, etc.) it can be ensured that the damage done to other remnants remains consistent.

- Do we use auto health regen so accidental damage doesn't cause unpredictability down the line?

- How much non-canon damage causes an unlink (if any)?

It's also important to decide how deviations from the canon are handled when it comes to *infliction.* For instance, if a remnant is supposed to kill another remnant, but the other remnant has slightly more health this time around, what happens? If we consider the amount of damage inflicted canon, then it will cause an unlink even if the other remnant still died (bad). However, only considering kills canon can fail to account for other scenarios where there was a scrimmage but no kill.

- Fights that end slightly early should not cause an unlink, but fights that end *too* early should.

- Auto health regen could help contain unknown variables within a single encounter.

Ultimately, it makes sense to consider a "attack" and its outcome canon. However, it could be hard to determine where an attack starts and ends. For instance, if someone starts spraying a minigun at their opponent, and only hit some of their shots but still manage to kill them, what happens? 

- Possible solution: consider the beginning of the attack and the final kill canon, and give a small time buffer for when the kill happens.

### Player intent

It will be important to come up with a system to distinguish player intent. For instance, a player firing randomly and accidentally hitting an explosive barrel needs to be treated differently than if they intended to shoot that barrel.

- Potentially based on the outcome of the explosion. Did it kill someone?

## What to store in a frame

- Frame actions

- Position

- Rotation

- Eye rotation

- Velocity

## Map / gameplay ideas

- Mounted minigun that can be destroyed.

- Elevator / moving walkway with two possible positions. Wrong position -> unlink

- Can only carry one weapon at a time, but can swap with weapons on the ground (from dead players or placed in map).
  
  - Missing weapon -> unlink

- Doors that only open if another agent is standing on or holding a button

- Explosive barrels that can only be triggered once per round.

# Events & Unlinks

I've been debating for a while on how to structure the event system in the code, how it ties to unlink, and how they both live in the Timeline Tree. I've come up with the following.

- **Events** have no impact on gameplay. They serve purely as the basis for unlinks. **Actions**, on the other hand, *do* affect gameplay as part of the animation, but have no effect on the timeline tree.

- **Only recorded events from previous rounds may lead to unlinks.** Unlinks *may not* be caused by body-blocking remnants or interfering in other ways that aren't pre-established. This limits the amount of complexity in the event system. Boolean checks of whether an event was successful or not means each event can directly lead to only two timeline branches.
  
  - `trigger_unlink` and `logic_unlink` entities allow the mapper to declare when a map entity *may* impact gameplay (elevators, doors, etc), therefore allowing map state to cause unlinks.
  
  - Exception for getting killed: see below.

- Each event type (kill, pickup, use, etc) has a dedicated C# class with the following:
  
  - Constructor receives all the data captured during initial recording.
    
    - References to persistent IDs rather than direct references to entities allow for persistence between rounds.
  
  - `IsValid` function detects whether the event can happen (or has happened) in the current context.
  
  - Serializer & deserializer for game save

- **Events are tested *before* their relevant actions.** Because many interactions don't overtly save their state in a universal format afterward (ex: `+use`), it makes more sense to check if an event *can* occur (the right entities are in the right place) than if if it *has* occurred. Failing the event will cause an unlink, so the related action won't be played by default.
  
  - An exception can be made for kill events and other complex events where it's non-trivial to predict the full outcome before it's simulated. In these situations, a check must be placed in the action to ensure it makes sense, and the event will check the results (is the player dead, etc).

- **Getting killed is not an event**. Although it's possible for getting killed to indirectly cause an unlink, unlike proper events, death events *cannot* directly cause branches in the timeline tree. 
  
  - If a remnant got killed in its original timeline but it doesn't now, it will appear to unlink. However, its original timeline from that point on is a stub, so its full timeline remains linear. Similarly, if a remnant gets killed too early, its animation will stop playback (indirectly causing unlinks), but there are no *new* actions to put in a branch.
  
  - It might be helpful to create a "dummy" death event so the unlink animation plays.

## Implementation Details

- Because we know every *potential* branch point as soon as the initial timeline is recorded, it might be helpful to split the animation at each event during recording. This way, its easier to assemble it into a recursive, branching tree structure.
  
  - Keeping animations separate and gatekeeping the next animation behind the event means actions won't be prematurely triggered in the event of a tickrate desync

- Animation tree could be a recursive data structure.
