#define _PMX_JOINWORLD_TPOSE_ONLY

using UnityEngine;
using System.Collections;
using BulletXNA;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.LinearMath;

using PMXShapeType          = MMD4MecanimBulletPMXCommon.PMXShapeType;
using PMXRigidBodyType      = MMD4MecanimBulletPMXCommon.PMXRigidBodyType;
using PMXBone               = MMD4MecanimBulletPMXBone;
using PMXModel              = MMD4MecanimBulletPMXModel;
using SimpleMotionState     = MMD4MecanimBulletPhysicsUtil.SimpleMotionState;
using KinematicMotionState  = MMD4MecanimBulletPhysicsUtil.KinematicMotionState;

public class MMD4MecanimBulletPMXRigidBody
{
	public PMXModel _model;
	public PMXBone _bone;
	
	public PMXModel model { get { return _model; } }
	public PMXBone bone { get { return _bone; } }
	
	int 				    _boneID = -1;
	uint	        	    _collisionGroupID;
	uint    	    	    _collisionMask;
	PMXShapeType	    	_shapeType;
	Vector3                 _shapeSize;
	Vector3			        _position;
	Vector3		    	    _rotation;
	float			    	_mass;
	float				    _linearDamping;
	float				    _angularDamping;
	float				    _restitution;
	float				    _friction;
	PMXRigidBodyType	    _rigidBodyType = PMXRigidBodyType.Kinematics;
	uint		    	    _additionalFlags;
	bool				    _isDisabled;
	bool                    _isKinematic;

	MMD4MecanimBulletPhysics.MMDModelRigidBodyProperty _mmdModelRigidBodyProperty;

	CollisionShape	        _shape;
	IMotionState	    	_motionState;
	RigidBody	    	    _bulletRigidBody;
	int	        		    _groupID;
	int     			    _groupMask;
	
	bool				    _noBone;
	IndexedMatrix		   	_boneTransform = IndexedMatrix.Identity;
	IndexedMatrix		    _boneTransformInverse = IndexedMatrix.Identity;
	IMotionState	    	_kinematicMotionState;
	bool                    _dirtyMotionState;
	
	DiscreteDynamicsWorld   _bulletWorld;
	
	public RigidBody bulletRigidBody { get { return _bulletRigidBody; } }
	public bool isDisabled { get { return _isDisabled; } }
	public bool isKinematic { get { return _isKinematic; } }
	public PMXRigidBodyType rigidBodyType { get { return _rigidBodyType; } }
	public int parentBoneID { get { return (_bone != null) ? _bone.parentBoneID : -1; } }

	~MMD4MecanimBulletPMXRigidBody()
	{
		Destroy();
	}
	
	public void Destroy()
	{
		LeaveWorld();
		
		if( _bulletRigidBody != null ) {
			_bulletRigidBody.Cleanup();
			_bulletRigidBody = null;
		}
		_motionState = null;
		_kinematicMotionState = null;
		if( _shape != null ) {
			_shape.Cleanup();
			_shape = null;
		}
		
		_bulletWorld = null;
	}
	
	public bool Import( MMD4MecanimCommon.BinaryReader binaryReader, MMD4MecanimBulletPhysics.MMDModelRigidBodyProperty mmdModelRigidBodyProperty )
	{
		if( !binaryReader.BeginStruct() ) {
			Debug.LogError("");
			return false;
		}
		
		_additionalFlags	= (uint)binaryReader.ReadStructInt();
		binaryReader.ReadStructInt(); // nameJp
		binaryReader.ReadStructInt(); // nameEn
		_boneID	        	= binaryReader.ReadStructInt();
		_collisionGroupID	= (uint)binaryReader.ReadStructInt();
		_collisionMask		= (uint)binaryReader.ReadStructInt();
		_shapeType			= (PMXShapeType)binaryReader.ReadStructInt();
		_rigidBodyType		= (PMXRigidBodyType)binaryReader.ReadStructInt();
		_shapeSize		    = binaryReader.ReadStructVector3();
		_position			= binaryReader.ReadStructVector3();
		_rotation			= binaryReader.ReadStructVector3();
		_mass				= binaryReader.ReadStructFloat();
		_linearDamping		= binaryReader.ReadStructFloat();
		_angularDamping		= binaryReader.ReadStructFloat();
		_restitution		= binaryReader.ReadStructFloat();
		_friction			= binaryReader.ReadStructFloat();
		_mmdModelRigidBodyProperty = mmdModelRigidBodyProperty;
		if( _mmdModelRigidBodyProperty == null ) {
			_mmdModelRigidBodyProperty = new MMD4MecanimBulletPhysics.MMDModelRigidBodyProperty();
		}
		
		if( !binaryReader.EndStruct() ) {
			Debug.LogError("");
			return false;
		}

		_isDisabled			= (_additionalFlags & 0x01) != 0;
		_isKinematic        = (_rigidBodyType == PMXRigidBodyType.Kinematics);
		
		if( _model != null ) {
			_shapeSize *= _model.modelToBulletScale;
			_position *= _model.modelToBulletScale;
		}
		
		// LH to RH
		_position.z = -_position.z;
		_rotation.x = -_rotation.x;
		_rotation.y = -_rotation.y;
		
		_boneTransform._basis = MMD4MecanimBulletPhysicsUtil.BasisRotationYXZ( ref _rotation );
		_boneTransform._origin = _position;
		_boneTransformInverse = _boneTransform.Inverse();
		
		_groupID = (int)(1 << (int)_collisionGroupID);
		_groupMask = (int)_collisionMask;
		
		if( _shapeType == PMXShapeType.Sphere ) {
			_shape = new SphereShape( _shapeSize.x );
		} else if( _shapeType == PMXShapeType.Box ) {
			_shape = new BoxShape( new IndexedVector3( _shapeSize ) );
		} else if( _shapeType == PMXShapeType.Capsule ) {
			_shape = new CapsuleShape( _shapeSize.x, _shapeSize.y );
		} else {
			return false;
		}
		
		_noBone = (_boneID < 0);
		if( _model != null ) {
			if( _noBone ) {
				_bone = _model.GetBone( 0 );
			} else {
				_bone = _model.GetBone( _boneID );
			}
		}
		
		if( _rigidBodyType != PMXRigidBodyType.Kinematics && !_noBone && _bone != null ) {
			_bone._rigidBody = this;
		}
		
		return true;
	}
	
	public void PreUpdateWorld()
	{
		if( _bone == null || _bulletRigidBody == null ) {
			return;
		}
		
		if( _rigidBodyType == PMXRigidBodyType.Kinematics ) {
			if( _motionState != null ) {
				((KinematicMotionState)_motionState).m_graphicsWorldTrans = _bone.worldTransform * _boneTransform;
			}
		} else if( _isDisabled || _isKinematic ) {
			if( _kinematicMotionState != null ) {
				((KinematicMotionState)_kinematicMotionState).m_graphicsWorldTrans = _bone.worldTransform * _boneTransform;
			}
		}
		
		if( _dirtyMotionState ) {
			_dirtyMotionState = false;
			_UpdateMotionState();
		}
	}
	
	public void ApplyTransformToBone()
	{
		if( _rigidBodyType == PMXRigidBodyType.Kinematics || _bone == null || _noBone || _isKinematic || _isDisabled ){
			return;
		}

		IndexedMatrix worldTransform = _bulletRigidBody.GetCenterOfMassTransform() * _boneTransformInverse;
		if( _rigidBodyType == PMXRigidBodyType.SimulatedAligned || !_bone.isBoneTranslate ) {
			if( _bone.parentBone != null ) {
				_bone.worldTransform._origin = _bone.parentBone.worldTransform * _bone.offset;
			}
			_bone.worldTransform._basis = worldTransform._basis;
		} else { // Simulated
			_bone.worldTransform = worldTransform;
		}
		
		_bone.NotifySetWorldTransform();
	}
	
	private bool _SetupBody()
	{
		if( _bulletRigidBody != null ) {
			return true;
		}
		if( _bone == null ) {
			return false;
		}
		
		float mass = 0.0f;
		IndexedVector3 localInertia = IndexedVector3.Zero;
		if( _shape != null ) {
			if( _rigidBodyType != PMXRigidBodyType.Kinematics && _mass != 0.0f ) {
				mass = _mass * _mmdModelRigidBodyProperty.mass;
				_shape.CalculateLocalInertia( mass, out localInertia );
			}
		}

#if _PMX_JOINWORLD_TPOSE_ONLY
		if( _model == null || _model.rootBone == null ) {
			return false;
		}
		IndexedMatrix startTransform = _boneTransform;
		startTransform._origin += _bone.baseOrigin + _model.rootBone.worldTransform._origin;
#else
		IndexedMatrix startTransform = _bone.worldTransform * _boneTransform;
#endif
		if( _rigidBodyType == PMXRigidBodyType.Simulated || _rigidBodyType == PMXRigidBodyType.SimulatedAligned ) {
			_motionState = new SimpleMotionState( ref startTransform );
			_kinematicMotionState = new KinematicMotionState( ref startTransform );
		} else {
			_rigidBodyType = PMXRigidBodyType.Kinematics;
			_motionState = new KinematicMotionState( ref startTransform );
			_kinematicMotionState = null;
		}
		
		RigidBodyConstructionInfo rbInfo = new RigidBodyConstructionInfo( mass, _motionState, _shape, localInertia );
		rbInfo.m_linearDamping	    = Mathf.Clamp( _linearDamping	* _mmdModelRigidBodyProperty.linearDamping,		0.0f, 1.0f );
		rbInfo.m_angularDamping	    = Mathf.Clamp( _angularDamping	* _mmdModelRigidBodyProperty.angularDamping,	0.0f, 1.0f );
		rbInfo.m_restitution	    = _restitution		* _mmdModelRigidBodyProperty.restitution;
		rbInfo.m_friction		    = _friction			* _mmdModelRigidBodyProperty.friction;
		rbInfo.m_additionalDamping  = _mmdModelRigidBodyProperty.isAdditionalDamping;
		
		_bulletRigidBody = new RigidBody( rbInfo );
		if( _bulletRigidBody != null ) {
			if( _rigidBodyType == PMXRigidBodyType.Kinematics ) {
				_bulletRigidBody.SetCollisionFlags(_bulletRigidBody.GetCollisionFlags() | BulletXNA.BulletCollision.CollisionFlags.CF_KINEMATIC_OBJECT);
			} else {
				if( _isKinematic || _isDisabled ) {
					if( _kinematicMotionState == null ) {
						return false;
					}
					_bulletRigidBody.ClearForces();
					_bulletRigidBody.SetMotionState(_kinematicMotionState);
					_bulletRigidBody.SetCollisionFlags(_bulletRigidBody.GetCollisionFlags() | BulletXNA.BulletCollision.CollisionFlags.CF_KINEMATIC_OBJECT);
				}
			}

			_bulletRigidBody.SetActivationState(ActivationState.DISABLE_DEACTIVATION);
		}
		
		return true;
	}
	
	public bool JoinWorld()
	{
		if( _bulletRigidBody == null ) {
			if( !_SetupBody() ) {
				Debug.LogError( "Warning: PMXRigidBody::JoinWorld(): Body is nothing." );
				return false;
			}
		}
		if( _bulletRigidBody == null || _bulletWorld != null || _model == null || _model.bulletWorld == null ) {
			Debug.LogError( "Warning: PMXRigidBody::JoinWorld(): Nothing." );
			return false;
		}
		
		_bulletWorld = _model.bulletWorld;
		_bulletWorld.AddRigidBody( _bulletRigidBody, (BulletXNA.BulletCollision.CollisionFilterGroups)_groupID, (BulletXNA.BulletCollision.CollisionFilterGroups)_groupMask );
		return true;
	}
	
	public void LeaveWorld()
	{
		if( _bulletRigidBody != null ) {
			if( _bulletWorld != null ) {
				_bulletWorld.RemoveRigidBody( _bulletRigidBody );
			}
			_bulletRigidBody.Cleanup();
			_bulletRigidBody = null;
		}
		
		_bulletWorld = null;
	}
	
	public void SetKinematic(bool isKinematic)
	{
		if( _rigidBodyType == PMXRigidBodyType.Kinematics ) {
			return;
		}
		
		if( _isKinematic != isKinematic ) {
			_isKinematic = isKinematic;
			_dirtyMotionState = true;
		}
	}
	
	public void SetDisabled(bool isDisabled)
	{
		if( _rigidBodyType == PMXRigidBodyType.Kinematics ) {
			return;
		}
		
		if( _isDisabled != isDisabled ) {
			_isDisabled = isDisabled;
			_dirtyMotionState = true;
		}
	}
	
	void _UpdateMotionState()
	{
		if( _bulletRigidBody == null ) {
			return;
		}
		
		if( _isKinematic || _isDisabled ) {
			if( _kinematicMotionState == null ) {
				return;
			}
			_bulletRigidBody.ClearForces();
			_bulletRigidBody.SetMotionState(_kinematicMotionState);
			_bulletRigidBody.SetCollisionFlags(_bulletRigidBody.GetCollisionFlags() | BulletXNA.BulletCollision.CollisionFlags.CF_KINEMATIC_OBJECT);
		} else {
			if( _kinematicMotionState == null || _motionState == null ) {
				return;
			}
			IndexedMatrix worldTransform;
			_kinematicMotionState.GetWorldTransform( out worldTransform );
			_motionState.SetWorldTransform( worldTransform );
			_bulletRigidBody.SetMotionState(_motionState);
			_bulletRigidBody.SetCollisionFlags(_bulletRigidBody.GetCollisionFlags() & ~BulletXNA.BulletCollision.CollisionFlags.CF_KINEMATIC_OBJECT);
			_bulletRigidBody.ClearForces();
		}
	}
}
