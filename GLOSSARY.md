# Object Pooling

Reuses scene objects made from a prefab instead of creating and destroying them, so gameplay can
spawn and despawn things every frame without allocation spikes.

## Language

**Pool**:
A named store of interchangeable instances of one prefab.
_Avoid_: object pool registry entry, pool key

**Pool name**:
The string that identifies a pool; one name means one pool, whatever type the caller asks for.
_Avoid_: pool key, type-qualified key

**Instance**:
One object a pool created from its prefab; it is either active (handed out) or inactive (stored).
_Avoid_: pooled object, tracked object, entry

**Spawn**:
Taking an instance out of its pool and making it active.
_Avoid_: get, build and get

**Despawn**:
Returning an active instance to its pool and making it inactive.
_Avoid_: release, return to pool

**Prewarm**:
Filling a pool with inactive instances ahead of time, without spawning them.
_Avoid_: build, create pool

**Destroy pool**:
Despawning every active instance of a pool, then destroying all its instances and the pool itself.
_Avoid_: clear pool, release and clear

**Snapshot**:
A point-in-time reading of one pool's counts and configuration.
_Avoid_: statistics, pool info

**Drop-in**:
A ready-made behaviour placed on a prefab that despawns its instance by itself when a condition is met.
_Avoid_: pooled component
