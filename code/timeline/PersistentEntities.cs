#nullable enable

using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClockBlockers.Timeline;

/// <summary>
/// For keeping track of entities across rounds.
/// </summary>
public static class PersistentEntities
{

	// TODO: check for ID intersections (extremely unlikely for random)

	/// <summary>
	/// Get an entity from its persistent ID.
	/// </summary>
	/// <typeparam name="TEntity">Type of the entity to look for.</typeparam>
	/// <param name="id">Persistent id.</param>
	/// <returns>The entity, or <c>null</c> if no entity with this type and ID was found.</returns>
	public static TEntity? GetEntity<TEntity>( string id ) where TEntity : Entity
	{
		foreach ( var ent in Entity.All.OfType<TEntity>() )
		{
			if ( ent.Components.TryGet<PersistentEntity>( out var component ) )
			{
				if ( component.ID == id ) return ent;
			}
			else if ( ent.HammerID == id )
			{
				return ent;
			}
		}
		return null;
	}

	/// <summary>
	/// Get an entity from its persistent ID.
	/// </summary>
	/// <typeparam name="TEntity">Type of the entity to look for.</typeparam>
	/// <param name="id">Persistent id.</param>
	/// <returns>The entity, or <c>null</c> if no entity with this type and ID was found.</returns>
	public static Entity? GetEntity( string id )
	{
		return GetEntity<Entity>( id );
	}

	/// <summary>
	/// Get the persistent ID of an entity.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="generate">If this entity doesn't have a persistent ID, generate one.</param>
	/// <returns>The ID</returns>
	public static string? GetPersistentID( this Entity? entity, bool generate = false )
	{
		if ( entity == null ) return null;
		if ( entity.HammerID != null ) return entity.HammerID;

		if ( entity.Components.TryGet<PersistentEntity>( out var component ) )
		{
			return component.ID;
		}
		else if ( generate )
		{
			component = entity.Components.Create<PersistentEntity>();
			Log.Warning( $"{entity} did not have a persistent ID. One will be generated, but it will likely be inconsistent between rounds." );
			return component.ID;
		}
		return null;
	}

	/// <summary>
	/// Get the persistent ID of an entity.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="generate">If this entity doesn't have a persistent ID, generate one.</param>
	/// <returns>The ID</returns>
	/// <exception cref="InvalidOperationException">If this entity doesn't have a persistent ID and <c>generate</c> is false.</exception>
	public static string GetPersistentIDOrThrow(this Entity entity, bool generate = false )
	{
		string? id = GetPersistentID( entity, generate );
		if (id == null)
		{
			throw new InvalidOperationException( $"{entity} does not have a persistent ID." );
		}
		return id;
	}

	/// <summary>
	/// Get the persistent ID of an entity, or generate it if one does not exist.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <returns>The ID.</returns>
	public static string GetPersistentIDOrCreate(this Entity entity)
	{
		return GetPersistentIDOrThrow( entity, true );
	}

	/// <summary>
	/// Manually set a dynamic persistent ID of an entity. Don't use unless you know what you're doing.
	/// </summary>
	/// <param name="entity">Entity to set.</param>
	/// <param name="id">ID to set</param>
	/// <returns><c>id</c></returns>
	public static string SetPersistentID( this Entity entity, string id )
	{
		if ( GetPersistentID( entity ) != null )
		{
			Log.Warning( $"Entity {entity} already has a persistent ID." );
		}

		var component = entity.Components.GetOrCreate<PersistentEntity>();
		component.ID = id;
		return component.ID;
	}

	/// <summary>
	/// Generate a random ID string.
	/// </summary>
	/// <returns></returns>
	public static string RandomID() => new RandomGenerator().RandomString( 8, true );
}

/// <summary>
/// An entity that was spawned dynamically but has a persistent ID across rounds.
/// </summary>
public partial class PersistentEntity : EntityComponent, ISingletonComponent
{
	/// <summary>
	/// The base ID of this entity (excluding 'dyn.')
	/// </summary>
	[Net]
	public string ID { get; set; } = PersistentEntities.RandomID();

}
