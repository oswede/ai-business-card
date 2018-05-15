using UnityEngine;
using System.Collections;
using BulletXNA;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.LinearMath;

public class MMD4MecanimBulletPhysicsWorld
{
	static readonly int maxFramePerSecond = 60;

	DefaultCollisionConfiguration		_collisionConfig;
	CollisionDispatcher					_dispatcher;
	AxisSweep3Internal					_broadphase;
	SequentialImpulseConstraintSolver	_solver;
	DiscreteDynamicsWorld				_world;

	int									_framePerSecond = 60;
	
	ArrayList 							_physicsEntityList = new ArrayList();
	
	public DiscreteDynamicsWorld bulletWorld {
		get {
			return _world;
		}
	}

	public struct CreateProperty
	{
		public int		framePerSecond;
		public float	gravityScale;
		public int		worldSolverInfoNumIterations;
	}
	
	public bool Create( ref CreateProperty createProperty )
	{
		Destroy();

		_framePerSecond = createProperty.framePerSecond;
		if( _framePerSecond <= 0 || _framePerSecond > maxFramePerSecond ) {
			_framePerSecond = maxFramePerSecond;
		}

		_collisionConfig = new DefaultCollisionConfiguration();
		_dispatcher = new CollisionDispatcher(_collisionConfig);
	
        IndexedVector3 worldMin = new IndexedVector3(-400, -400, -400);
        IndexedVector3 worldMax = -worldMin;
        _broadphase = new AxisSweep3Internal(ref worldMin, ref worldMax, 0xfffe, 0xffff, 16384, null, false);
        _solver = new SequentialImpulseConstraintSolver();
        _world = new DiscreteDynamicsWorld(_dispatcher, _broadphase, _solver, _collisionConfig);
		
        IndexedVector3 worldGravity = new IndexedVector3(0.0f, -9.8f * createProperty.gravityScale, 0.0f);
        _world.SetGravity(ref worldGravity);
		
		if( _world.GetSolverInfo() != null ) {
			if( createProperty.worldSolverInfoNumIterations <= 0 ) {
				_world.GetSolverInfo().m_numIterations = (int)(10 * 60 / _framePerSecond);
			} else {
				_world.GetSolverInfo().m_numIterations = createProperty.worldSolverInfoNumIterations;
			}
		}
		
		return true;
	}

	public void Destroy()
	{
		while( _physicsEntityList.Count > 0 ) {
			int index = _physicsEntityList.Count - 1;
			MMD4MecanimBulletPhysicsEntity physicsEntity = (MMD4MecanimBulletPhysicsEntity)(_physicsEntityList[index]);
			_physicsEntityList.RemoveAt( index );
			physicsEntity.LeaveWorld();
		}
		
		if( _world != null ) {
			_world.Cleanup();
			_world = null;
		}
		if( _solver != null ) {
			_solver.Cleanup();
			_solver = null;
		}
		if( _broadphase != null ) {
			_broadphase.Cleanup();
			_broadphase = null;
		}
		if( _dispatcher != null ) {
			_dispatcher.Cleanup();
			_dispatcher = null;
		}
		if( _collisionConfig != null ) {
			_collisionConfig.Cleanup();
			_collisionConfig = null;
		}
	}

	public void Update( float deltaTime )
	{
		/* Check for World Reset. */
		float resetWorldTime = 0.0f;
		for( int i = 0; i < _physicsEntityList.Count; ++i ) {
			resetWorldTime = Mathf.Max( ((MMD4MecanimBulletPhysicsEntity)_physicsEntityList[i])._GetResetWorldTime(), resetWorldTime );
		}
	
		if( resetWorldTime > 0.0f ) {
			for( int i = 0; i < _physicsEntityList.Count; ++i ) {
				((MMD4MecanimBulletPhysicsEntity)_physicsEntityList[i])._PreResetWorld();
			}
		}
	
		_Update( deltaTime );
	
		if( resetWorldTime > 0.0f ) {
			for( int i = 0; i < _physicsEntityList.Count; ++i ) {
				((MMD4MecanimBulletPhysicsEntity)_physicsEntityList[i])._StepResetWorld( 0.0f );
			}
	
			float elapsedTime = 0.0f;
			bool finalStep = false;
			while( elapsedTime < resetWorldTime && !finalStep ) {
				_Update( 1.0f / 30.0f );
				elapsedTime += 1.0f / 30.0f;
				if( elapsedTime > resetWorldTime ) {
					elapsedTime = resetWorldTime;
					finalStep = true;
				}
				for( int i = 0; i < _physicsEntityList.Count; ++i ) {
					((MMD4MecanimBulletPhysicsEntity)_physicsEntityList[i])._StepResetWorld( elapsedTime );
				}
			}
	
			for( int i = 0; i < _physicsEntityList.Count; ++i ) {
				((MMD4MecanimBulletPhysicsEntity)_physicsEntityList[i])._PostResetWorld();
			}
		}
	}

	public void JoinWorld( MMD4MecanimBulletPhysicsEntity physicsEntity )
	{
		if( physicsEntity == null ) {
			Debug.LogError("");
			return;
		}
		if( physicsEntity.physicsWorld != null ) {
			Debug.LogError("");
			return;
		}
	
		physicsEntity._physicsWorld = this;
		if( !physicsEntity._JoinWorld() ) {
			Debug.LogError("");
			physicsEntity._physicsWorld = null;
		} else {
			_physicsEntityList.Add( physicsEntity );
		}
	}

	void _Update( float deltaTime )
	{
		if( _world != null ) {
			for( int i = 0; i < _physicsEntityList.Count; ++i ) {
				((MMD4MecanimBulletPhysicsEntity)_physicsEntityList[i])._PreUpdateWorld();
			}
			_world.StepSimulation( deltaTime, 2 );
			for( int i = 0; i < _physicsEntityList.Count; ++i ) {
				((MMD4MecanimBulletPhysicsEntity)_physicsEntityList[i])._PostUpdateWorld();
			}
		}
	}
	
	// from MMD4MecanimBulletPhysicsEntity
	public void _RemoveEntity( MMD4MecanimBulletPhysicsEntity physicsEntity )
	{
		for( int i = 0; i < _physicsEntityList.Count; ++i ) {
			if( (MMD4MecanimBulletPhysicsEntity)_physicsEntityList[i] == physicsEntity ) {
				_physicsEntityList.RemoveAt( i );
				physicsEntity._physicsWorld = null;
				break;
			}
		}
	}
}
