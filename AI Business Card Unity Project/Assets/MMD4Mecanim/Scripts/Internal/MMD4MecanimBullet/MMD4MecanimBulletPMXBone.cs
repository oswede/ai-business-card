using UnityEngine;
using System.Collections;
using BulletXNA;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.LinearMath;

using PMXFileType       = MMD4MecanimBulletPMXCommon.PMXFileType;
using PMDBoneType       = MMD4MecanimBulletPMXCommon.PMDBoneType;
using PMXBoneFlag		= MMD4MecanimBulletPMXCommon.PMXBoneFlag;
using PMXRigidBodyType  = MMD4MecanimBulletPMXCommon.PMXRigidBodyType;
using PMXModel          = MMD4MecanimBulletPMXModel;
using PMXRigidBody      = MMD4MecanimBulletPMXRigidBody;
using PMXBone           = MMD4MecanimBulletPMXBone;

public class MMD4MecanimBulletPMXBone
{
	public PMXModel _model;
	public PMXRigidBody _rigidBody;
	
	public PMXModel model { get { return _model; } }
	public PMXRigidBody rigidBody { get { return _rigidBody; } }
	
	// Base Data
	int							_boneID;
	int							_parentBoneID;
	PMDBoneType					_pmdBoneType;
	PMXBone                     _parentBone;
	IndexedVector3			    _baseOrigin;
	uint						_additionalFlags;
	PMXBoneFlag					_boneFlags = PMXBoneFlag.None;
	
	// Additional Data
	IndexedVector3				_offset;
	IndexedVector3				_offsetUnityScale;
	bool						_isRootBone;
	
	// Work
	public IndexedMatrix	    worldTransform = IndexedMatrix.Identity;
	public IndexedBasisMatrix   invserseWorldBasis = IndexedBasisMatrix.Identity;
	private bool                _isUpdatedWorldTransform;
	private bool                _isDirtyInverseWorldBasis;
	
	public IndexedMatrix       	moveWorldTransform;
	private bool		    	_isMovingOnResetWorld;
	private bool			    _isMovingWorldTransform;
	private Vector3     		_moveSourcePosition;
	private Vector3		        _moveDestPosition;
	private Quaternion          _moveSourceRotation;
	private Quaternion          _moveDestRotation;
	
	public int boneID                   	{ get { return _boneID; } }
	public int parentBoneID			    	{ get { return _parentBoneID; } }
	public PMDBoneType pmdBoneType	    	{ get { return _pmdBoneType; } }
	public bool isRootBone					{ get { return _isRootBone; } set { _isRootBone = value; } }
	
	public IndexedVector3 baseOrigin		{ get { return _baseOrigin; } }
	public IndexedVector3 offset			{ get { return _offset; } }
	public IndexedVector3 offsetUnityScale	{ get { return _offsetUnityScale; } }
	
	public PMXBone parentBone				{ get { return _parentBone; } }
	
	public void Destroy()
	{
		_model = null;
		_rigidBody = null;
		_parentBone = null;
	}

	public bool isBoneTranslate
	{
		get {
			return (_boneFlags & PMXBoneFlag.Translate) != 0;
		}
	}

	public bool Import( int boneID, MMD4MecanimCommon.BinaryReader binaryReader )
	{
		if( !binaryReader.BeginStruct() ) {
			Debug.LogError("BeginStruct() failed.");
			return false;
		}
		
		_boneID				= boneID;
		_additionalFlags	= (uint)binaryReader.ReadStructInt();
		binaryReader.ReadStructInt(); // nameJp
		binaryReader.ReadStructInt(); // nameEn
		binaryReader.ReadStructInt(); // skeletonName
		_parentBoneID		= binaryReader.ReadStructInt();
		binaryReader.ReadStructInt(); // sortedBoneID
		binaryReader.ReadStructInt(); // orderedBoneID
		binaryReader.ReadStructInt(); // originalPanretBoneID
		binaryReader.ReadStructInt(); // originalSortedBoneID
		_baseOrigin			= binaryReader.ReadStructVector3();
		
		if( _model != null ) {
			if( _model.fileType == PMXFileType.PMD ) {
				_pmdBoneType	= (PMDBoneType)binaryReader.ReadStructInt();
				//binaryReader.ReadStructInt(); // childBoneID
				//binaryReader.ReadStructInt(); // targetBoneID / rotateCoef(pmdBoneType == FollowRotate)

				switch( _pmdBoneType ) {
				case PMDBoneType.Rotate:
					_boneFlags = PMXBoneFlag.Visible | PMXBoneFlag.Rotate;
					break;
				case PMDBoneType.RotateAndMove:
					_boneFlags = PMXBoneFlag.Visible | PMXBoneFlag.Rotate | PMXBoneFlag.Translate;
					break;
				case PMDBoneType.IKDestination:
					_boneFlags = PMXBoneFlag.Visible | PMXBoneFlag.Rotate | PMXBoneFlag.Translate | PMXBoneFlag.IK;
					break;
				case PMDBoneType.Unknown:
					_boneFlags = 0;
					break;
				case PMDBoneType.UnderIK:
					_boneFlags = PMXBoneFlag.Visible | PMXBoneFlag.Rotate | PMXBoneFlag.IKChild;
					break;
				case PMDBoneType.UnderRotate:
					_boneFlags = PMXBoneFlag.Visible | PMXBoneFlag.Rotate | PMXBoneFlag.InherenceRotate;
					break;
				case PMDBoneType.IKTarget:
					_boneFlags = PMXBoneFlag.Destination;
					break;
				case PMDBoneType.NoDisp:
					_boneFlags = PMXBoneFlag.InherenceLocal;
					break;
				case PMDBoneType.Twist:
					_boneFlags = PMXBoneFlag.Visible | PMXBoneFlag.Rotate | PMXBoneFlag.FixedAxis;
					break;
				case PMDBoneType.FollowRotate:
					_boneFlags = PMXBoneFlag.Rotate | PMXBoneFlag.InherenceRotate;
					break;
				default:
					_boneFlags = 0;
					break;
				}
			} else if( _model.fileType == PMXFileType.PMX ) {
				binaryReader.ReadStructInt(); // transformLayerID
				_boneFlags		= (PMXBoneFlag)binaryReader.ReadStructInt();
			}
		}
		
		if( !binaryReader.EndStruct() ) {
			Debug.LogError("EndStruct() failed.");
			return false;
		}
		
		//_isLimitAngleX	= (_additionalFlags & 0x01u) != 0;
		//this.isRigidBody	= (_additionalFlags & 0x02u) != 0;
		//this.isKinematic	= (_additionalFlags & 0x04u) != 0;
		_isRootBone			= ((_additionalFlags & 0xff000000u) == 0x80000000u);
		//_isDummyCharBone	= ((_additionalFlags & 0xff000000u) == 0xc0000000u);
		
		if( _model != null ) {
			_baseOrigin *= _model.modelToBulletScale;
		}
		
		_baseOrigin.Z = -_baseOrigin.Z; // LH to RH
		this.worldTransform._origin = _baseOrigin;
		return true;
	}
	
	public bool PostfixImport()
	{
		if( _model == null ) {
			return false;
		}
		
		bool r = true;
		if( _parentBoneID >= 0 ) {
			_parentBone = _model.GetBone(_parentBoneID);
			if( _parentBone == null ) {
				r = false;
			}
		}
		
		if( _parentBone != null ) {
			_offset = _baseOrigin - _parentBone.baseOrigin;
		} else {
			_offset = _baseOrigin;
		}
		
		_offsetUnityScale = _offset * _model.bulletToUnityScale;
		return r;
	}
	
	public bool isRigidBody {
		get {
			return _rigidBody != null;
		}
	}
	
	public bool isRigidBodyKinematic {
		get {
			if( _rigidBody != null ) {
				return _rigidBody.isKinematic;
			}
			
			return false;
		}
	}
	
	public bool isRigidBodyDisabled {
		get {
			if( _rigidBody != null ) {
				return _rigidBody.isDisabled;
			}
			
			return false;
		}
	}
	
	public PMXRigidBodyType rigidBodyType {
		get {
			if( _rigidBody != null ) {
				return _rigidBody.rigidBodyType;
			}
			
			return PMXRigidBodyType.Kinematics;
		}
	}
	
	public bool isRigidBodySimulated {
		get {
			return this.rigidBodyType != PMXRigidBodyType.Kinematics;
		}
	}
	
	public void SetRigidBodyKinematic(bool isKinematic)
	{
		if (_model != null && _rigidBody != null) {
			_rigidBody.SetKinematic(isKinematic);
		}
	}
	
	public void SetRigidBodyDisabled(bool isDisabled)
	{
		if (_model != null && _rigidBody != null) {
			_rigidBody.SetDisabled(isDisabled);
		}
	}
	
	public void ResetTransform()
	{
		this.worldTransform._basis = IndexedBasisMatrix.Identity;
		this.worldTransform._origin = _baseOrigin;
		_isUpdatedWorldTransform = false;
		_isDirtyInverseWorldBasis = true;
	}
	
	public void NotifySetWorldTransform()
	{
		_isUpdatedWorldTransform = true;
		_isDirtyInverseWorldBasis = true;
	}
	
	public void NotifySetMoveWorldTransform()
	{
		_isMovingOnResetWorld = true;
	}
	
	public void _PerformWorldTransform()
	{
		if( _isUpdatedWorldTransform ) {
			return;
		}
		
		if( _parentBone != null ) {
			if( _parentBone._isUpdatedWorldTransform ) {
				_isUpdatedWorldTransform = true;
				_isDirtyInverseWorldBasis = true;
				this.worldTransform._basis = IndexedBasisMatrix.Identity;
				this.worldTransform._origin = _offset;
				this.worldTransform = _parentBone.worldTransform * this.worldTransform;
			}
		} else {
			_isUpdatedWorldTransform = true;
			_isDirtyInverseWorldBasis = true;
			this.worldTransform._basis = IndexedBasisMatrix.Identity;
			this.worldTransform._origin = _offset;
		}
	}
	
	public void CleanupUpdatedWorldTransform()
	{
		_isUpdatedWorldTransform = false;
	}
	
	public void PrecheckInverseWorldBasisTransform()
	{
		if( _isDirtyInverseWorldBasis ) {
			_isDirtyInverseWorldBasis = false;
			this.invserseWorldBasis = this.worldTransform._basis.Transpose();
		}
	}
	
	public void PrepareMoveWorldTransform()
	{
		if( _isUpdatedWorldTransform ) {
			return;
		}
		if( _parentBone != null ) {
			_parentBone.PrepareMoveWorldTransform();
		}
		
		if( _isMovingOnResetWorld && _isRootBone ) {
			this.worldTransform._basis = IndexedBasisMatrix.Identity;
			this.worldTransform._origin = this.moveWorldTransform._origin;
			_isUpdatedWorldTransform = true;
		}
		
		_PerformWorldTransform();
		
		if( _isMovingOnResetWorld ) {
			if( _parentBone != null && _parentBone._isMovingOnResetWorld ) {
				_isMovingWorldTransform = false;
				IndexedMatrix sourceTransform = parentBone.worldTransform.Inverse() * this.worldTransform;
				IndexedMatrix destTransform = parentBone.moveWorldTransform.Inverse() * this.moveWorldTransform;
				_moveSourcePosition = sourceTransform._origin;
				_moveDestPosition = destTransform._origin;
				_moveSourceRotation = sourceTransform._basis.GetRotation();
				_moveDestRotation = destTransform._basis.GetRotation();
			} else {
				_isMovingWorldTransform = true;
				_moveSourcePosition = this.worldTransform._origin;
				_moveDestPosition = this.moveWorldTransform._origin;
				_moveSourceRotation = this.worldTransform._basis.GetRotation();
				_moveDestRotation = this.moveWorldTransform._basis.GetRotation();
			}
		}
	}
	
	public void PerformMoveWorldTransform( float r )
	{
		if( _isUpdatedWorldTransform ) {
			return;
		}
		if( _parentBone != null ) {
			_parentBone.PerformMoveWorldTransform( r );
		}
		
		if( _isMovingOnResetWorld ) {
			if( r == 1.0f ) {
				this.worldTransform = this.moveWorldTransform;
				_isUpdatedWorldTransform = true;
				_isDirtyInverseWorldBasis = true;
			} else {
				if( _isMovingWorldTransform ) { // World
					this.worldTransform._origin = Vector3.Lerp( _moveSourcePosition, _moveDestPosition, r );
					this.worldTransform._basis.SetRotation( Quaternion.Lerp( _moveSourceRotation, _moveDestRotation, r ) );
					_isUpdatedWorldTransform = true;
					_isDirtyInverseWorldBasis = true;
				} else { // Local
					this.worldTransform._origin = Vector3.Lerp( _moveSourcePosition, _moveDestPosition, r );
					this.worldTransform._basis.SetRotation( Quaternion.Lerp( _moveSourceRotation, _moveDestRotation, r ) );
					this.worldTransform = _parentBone.worldTransform * this.worldTransform;
					_isUpdatedWorldTransform = true;
					_isDirtyInverseWorldBasis = true;
				}
			}
		} else {
			_PerformWorldTransform();
		}
	}
	
	public void CleanupMoveWorldTransform()
	{
		_isMovingOnResetWorld = false;
	}
	
}
