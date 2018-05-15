using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using MorphCategory		= MMD4MecanimData.MorphCategory;
using MorphType			= MMD4MecanimData.MorphType;
using MorphData			= MMD4MecanimData.MorphData;
using MorphMotionData	= MMD4MecanimData.MorphMotionData;
using BoneData			= MMD4MecanimData.BoneData;
using FileType			= MMD4MecanimData.FileType;
using PMDBoneType		= MMD4MecanimData.PMDBoneType;
using PMXBoneFlags		= MMD4MecanimData.PMXBoneFlags;

public class MMD4MecanimBone : MonoBehaviour
{
	public MMD4MecanimModel	model;
	public int				boneID = -1;
	public bool				ikEnabled = false;
	public float			ikWeight = 1.0f;
	public GameObject		ikGoal;

	FileType				_fileType = FileType.None;
	BoneData				_boneData;
	MMD4MecanimBone			_rootBone; // localInherence only.
	MMD4MecanimBone			_parentBone;
	MMD4MecanimBone			_originalParentBone;
	MMD4MecanimBone			_inherenceParentBone;
	Vector3					_originalLocalPosition	= Vector3.zero;			// for modifiedHierarchy
	Quaternion				_originalLocalRotation	= Quaternion.identity;	// for modifiedHierarchy
	Vector3					_userEulerAngles		= Vector3.zero;
	Quaternion				_userRotation			= Quaternion.identity;
	Quaternion				_inherenceRotation		= Quaternion.identity;

	Quaternion				_storedLocalRotation	= Quaternion.identity;	// for _changedExternal / IK
	bool					_changedExternal;
	uint					_updatedTransform;
	uint					_parentUpdatedTransform;

	[NonSerialized]
	public Vector3			ikEulerAngles;

	public BoneData boneData { get { return _boneData; } }

	[NonSerialized]
	public bool isModelControl;
	[NonSerialized]
	public bool isIKDepended;
	[NonSerialized]
	public bool isOverwriteAfterIK;
	[NonSerialized]
	public float feedbackIKWeight;

	public bool isRigidBodySimulated
	{
		get {
			// Pending: Feedback realtime paremeter.
			return _boneData != null && _boneData.isRigidBody && !_boneData.isKinematic;
		}
	}

	public Vector3 userEulerAngles {
		get {
			return _userEulerAngles;
		}
		set {
			if( MMD4MecanimCommon.FuzzyZero( value ) ) {
				_userRotation = Quaternion.identity;
				_userEulerAngles = Vector3.zero;
			} else {
				_userRotation = Quaternion.Euler( value );
				_userEulerAngles = value;
			}
		}
	}

	public Quaternion userRotation {
		get {
			return _userRotation;
		}
		set {
			if( MMD4MecanimCommon.FuzzyIdentity( value ) ) { // Optimized: userRotation == (0,0,0)
				_userRotation = Quaternion.identity;
				_userEulerAngles = Vector3.zero;
			} else {
				_userRotation = value;
				_userEulerAngles = value.eulerAngles;
			}
		}
	}

	public Quaternion rotation {
		get {
			return this.gameObject.transform.rotation;
		}
	}

	public Quaternion localRotation {
		get {
			return this.gameObject.transform.localRotation;
		}
	}

	public Matrix4x4 originalLocalToWorldMatrix {
		get {
			if( _isModifiedHierarchy ) {
				Matrix4x4 parentLocalToWorldMatrix = _originalParentBone.originalLocalToWorldMatrix;
				Matrix4x4 localMatrix = Matrix4x4.TRS( _originalLocalPosition, _originalLocalRotation, Vector3.one );
				return parentLocalToWorldMatrix * localMatrix;
			} else {
				return this.gameObject.transform.localToWorldMatrix;
			}
		}
	}

	public Matrix4x4 originalWorldToLocalMatrix {
		get {
			if( _isModifiedHierarchy ) {
				Matrix4x4 localToWorldMatrix = this.originalLocalToWorldMatrix;
				return localToWorldMatrix.inverse;
			} else {
				return this.gameObject.transform.worldToLocalMatrix;
			}
		}
	}

	public Quaternion originalLocalRotation {
		get {
			if( _isModifiedHierarchy ) {
				return _originalLocalRotation;
			} else {
				return this.gameObject.transform.localRotation;
			}
		}
	}

	bool _isModifiedHierarchy
	{
		get {
			if( _boneData.parentBoneID == _boneData.originalParentBoneID ||
			    _boneData.parentBoneID == -1 || _boneData.originalParentBoneID == -1 ||
			    _parentBone == null || _originalParentBone == null ) {
				return false;
			}
			
			return true;
		}
	}

	public void Setup()
	{
		if( this.model == null || this.model.modelData == null || this.model.modelData.boneDataList == null ||
		    this.boneID < 0 || this.boneID >= this.model.modelData.boneDataList.Length ) {
			return;
		}

		_fileType = this.model.modelData.fileType;
		_boneData = this.model.modelData.boneDataList[this.boneID];
	}
	
	public void Bind()
	{
		if( this.model == null || _boneData == null ) {
			#if MMD4MECANIM_DEBUG
			Debug.LogError("");
			#endif
			return;
		}

		if( _boneData.parentBoneID != -1 ) {
			_parentBone = this.model.GetBone( _boneData.parentBoneID );
			#if MMD4MECANIM_DEBUG
			if( _parentBone == null ) {
				Debug.LogWarning("Not found parentBoneID:" + _boneData.parentBoneID + " boneID:" + this.boneID);
			}
			#endif
		}

		if( _boneData.originalParentBoneID != -1 ) {
			_originalParentBone = this.model.GetBone( _boneData.originalParentBoneID );
			#if MMD4MECANIM_DEBUG
			if( _originalParentBone == null ) {
				Debug.LogWarning("Not found originalParentBoneID:" + _boneData.originalParentBoneID + " boneID:" + this.boneID);
			}
			#endif
		}

		_originalLocalPosition = this.gameObject.transform.localPosition;
		if( _boneData.parentBoneID == _boneData.originalParentBoneID ||
		    _boneData.parentBoneID == -1 || _boneData.originalParentBoneID == -1 ) {
			// Nothing.
		} else {
			if( _originalParentBone != null && _originalParentBone.boneData != null &&
			    _boneData != null && this.model != null && this.model.modelData != null ) {
				_originalLocalPosition = _boneData.baseOrigin - _originalParentBone.boneData.baseOrigin;
				float modelToUnityScale = this.model.modelData.vertexScale * this.model.modelData.importScale;
				_originalLocalPosition *= modelToUnityScale;
			}
		}

		if( this.model != null ) {
			if( _fileType == FileType.PMD ) {
				if( _boneData.pmdBoneType == PMDBoneType.UnderRotate ) {
					if( _boneData.targetBoneID != -1 ) {
						_inherenceParentBone = this.model.GetBone( _boneData.targetBoneID );
						#if MMD4MECANIM_DEBUG
						if( _inherenceParentBone == null ) {
							Debug.LogWarning("Not found targetBoneID:" + _boneData.targetBoneID + " boneID:" + this.boneID);
						}
						#endif
					}
				} else if( _boneData.pmdBoneType == PMDBoneType.FollowRotate ) {
					if( _boneData.childBoneID != -1 ) {
						_inherenceParentBone = this.model.GetBone( _boneData.childBoneID );
						#if MMD4MECANIM_DEBUG
						if( _inherenceParentBone == null ) {
							Debug.LogWarning("Not found childBoneID:" + _boneData.childBoneID + " boneID:" + this.boneID);
						}
						#endif
					}
				}
			} else if( _fileType == FileType.PMX ) {
				if( (_boneData.pmxBoneFlags & (PMXBoneFlags.InherenceTranslate | PMXBoneFlags.InherenceRotate)) != PMXBoneFlags.None ) {
					if( _boneData.inherenceParentBoneID != -1 ) {
						_inherenceParentBone = this.model.GetBone( _boneData.inherenceParentBoneID );
						#if MMD4MECANIM_DEBUG
						if( _inherenceParentBone == null ) {
							Debug.LogWarning("Not found inherenceParentBoneID:" + _boneData.inherenceParentBoneID + " boneID:" + this.boneID);
						}
						#endif
					}
					if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceLocal) != PMXBoneFlags.None ) {
						_rootBone = this.model.GetRootBone();
						#if MMD4MECANIM_DEBUG
						if( _rootBone == null ) {
							Debug.LogWarning("Not found rootBone: boneID:" + this.boneID);
						}
						#endif
					}
				}
			}
		}
	}
	
	public void Destroy()
	{
		_boneData = null;
		_parentBone = null;
		_originalParentBone = null;
		_inherenceParentBone = null;
	}
	
	//----------------------------------------------------------------------------------------------------------------

	public void ClearFlags()
	{
		this.isModelControl		= false;
		this.isIKDepended		= false;
		this.isOverwriteAfterIK	= false;
		this.feedbackIKWeight	= 0.0f;
	}

	public void MarkModelControl()
	{
		if( !this.isRigidBodySimulated ) { // Don't process isRigidBodySimulated
			this.isModelControl |= this.isIKDepended;
			this.isModelControl |= (_inherenceParentBone != null);
		}
	}

	public void MarkOverwriteAfterIK()
	{
		if( !this.isRigidBodySimulated ) { // Don't process isRigidBodySimulated
			if( _inherenceParentBone != null ) {
				this.isOverwriteAfterIK =
					_inherenceParentBone.isIKDepended ||
					_inherenceParentBone.isOverwriteAfterIK;
				this.feedbackIKWeight = Mathf.Max(this.feedbackIKWeight, _inherenceParentBone.feedbackIKWeight);
			}
		}
	}

	//----------------------------------------------------------------------------------------------------------------

	// Check updated parent bone.
	uint _GetParentUpdatedTransform()
	{
		if( _isModifiedHierarchy ) {
			return _parentBone._updatedTransform + _originalParentBone._updatedTransform;
		}
		
		return 0;
	}

	// Every bone at more than once.(Deep check hierarchy.)
	public bool PostfixTransfrom()
	{
		if( this.isRigidBodySimulated ) {
			return false; // Don't process isRigidBodySimulated
		}
		
		if( _isModifiedHierarchy ) {
			uint parentUpdatedTransform = _GetParentUpdatedTransform();
			if( _parentUpdatedTransform != parentUpdatedTransform ) {
				_SetLocalRotation( _originalLocalRotation );
				_parentUpdatedTransform = parentUpdatedTransform;
				++_updatedTransform;
				return true;
			}
		}
		
		return false;
	}

	//----------------------------------------------------------------------------------------------------------------

	// Every bone at once.
	public void ResetTransform()
	{
		_changedExternal		= false; // for Bone Inherence
		_updatedTransform		= 0; // for _GetParentUpdatedTransform() / PostfixTransfrom()
		_parentUpdatedTransform	= 0; // for _GetParentUpdatedTransform() / PostfixTransfrom()
		_inherenceRotation		= Quaternion.identity;

		if( this.isRigidBodySimulated ) {
			return; // Don't process isRigidBodySimulated
		}

		if( _isModifiedHierarchy && !this.isModelControl ) { // _isModifiedHierarchy( Not IK & Inherence bone. )
			_originalLocalRotation = _ComputeLocalRotation(); // for PostfixTransform()
			// Memo: this.isModelControl is overwritten _originalLocalRotation in PrepareTransfrom()
		}
	}

	// Every bone at once.
	public void ResetTransform2()
	{
		if( this.isRigidBodySimulated ) {
			return; // Don't process isRigidBodySimulated
		}

		if( this.isModelControl ) { // IK or Inherence
			_SetLocalRotation( Quaternion.identity );
			_parentUpdatedTransform = _GetParentUpdatedTransform();
			++_updatedTransform;

			// Require: PostfixTransfrom() for children.
		}
	}

	//----------------------------------------------------------------------------------------------------------------

	// Every bone at once.
	public void StoreLocalTransform()
	{
		if( this.isRigidBodySimulated ) {
			return; // Don't process isRigidBodySimulated
		}

		if( this.isModelControl ) { // IK or Inherence
			// for MarkChangedExternal()
			_storedLocalRotation = this.gameObject.transform.localRotation;
		}
	}

	// Every bone at once.
	public void MarkChangedExternal()
	{
		if( this.isRigidBodySimulated ) {
			return; // Don't process isRigidBodySimulated
		}

		if( this.isModelControl ) { // IK or Inherence
			// If changed external, skip bone inherence.
			Quaternion localRotation = this.gameObject.transform.localRotation;
			_changedExternal = (localRotation != _storedLocalRotation);
			_storedLocalRotation = localRotation;
		}
	}

	//----------------------------------------------------------------------------------------------------------------

	public bool IsUserTransform()
	{
		return this.userRotation != Quaternion.identity;
	}

	public void PrepareLateTransform()
	{
		if( this.isRigidBodySimulated ) {
			return; // Don't process isRigidBodySimulated
		}

		if( _isModifiedHierarchy ) { // ModifiedHierarchy
			_originalLocalRotation = _ComputeLocalRotation(); // for PostfixTransform()
		}
	}

	public void PerformUserTransform()
	{
		if( this.isRigidBodySimulated ) {
			return; // Don't process isRigidBodySimulated
		}

		if( this.userRotation != Quaternion.identity ) {
			if( _isModifiedHierarchy ) {
				_SetLocalRotation( MMD4MecanimCommon.FastMul( this.userRotation, _originalLocalRotation ) );
			} else {
				_SetLocalRotation( MMD4MecanimCommon.FastMul( this.userRotation, _ComputeLocalRotation() ) );
			}
			_parentUpdatedTransform = _GetParentUpdatedTransform();
			++_updatedTransform;

			// Require: PostfixTransfrom() for children.
		}
	}

	//----------------------------------------------------------------------------------------------------------------

	public void PrepareIKTransform()
	{
		if( this.isRigidBodySimulated ) {
			return; // Don't process isRigidBodySimulated
		}

		if( this.isIKDepended || this.isOverwriteAfterIK ) {
			_storedLocalRotation = this.originalLocalRotation;
		}
	}

	public void ApplyIKRotation( Quaternion ikRotation )
	{
		_ApplyLocalRotation( ikRotation );
		_parentUpdatedTransform = _GetParentUpdatedTransform();
		++_updatedTransform;
	}
	
	public void SetLocalRotationFromIK( Quaternion localRotation )
	{
		_SetLocalRotation( localRotation );
		_parentUpdatedTransform = _GetParentUpdatedTransform();
		++_updatedTransform;
	}

	public void PostfixIKTransform()
	{
		if( this.isRigidBodySimulated ) {
			return; // Don't process isRigidBodySimulated
		}

		if( this.isIKDepended || this.isOverwriteAfterIK ) {
			if( this.feedbackIKWeight != 1.0f ) {
				_SetLocalRotation( Quaternion.Slerp( _storedLocalRotation, this.originalLocalRotation, this.feedbackIKWeight ) );
				_parentUpdatedTransform = _GetParentUpdatedTransform();
				++_updatedTransform;
			}
		}
	}

	//----------------------------------------------------------------------------------------------------------------

	public void PerformFullTransform()
	{
		if( this.model == null || _boneData == null ) {
			return;
		}
		if( !this.isModelControl ) {
			return;
		}
		if( this.isIKDepended || this.isRigidBodySimulated ) {
			return;
		}
		if( _changedExternal ) {
			return; // Skip if updated animator.
		}

		_PerformFullTransform( false );
	}

	public void PerformFullTransformAfterIK()
	{
		if( this.model == null || _boneData == null ) {
			return;
		}
		if( !this.isModelControl ) {
			return;
		}
		if( this.isIKDepended || this.isRigidBodySimulated ) {
			return;
		}
		if( !this.isOverwriteAfterIK ) {
			return;
		}

		_PerformFullTransform( true );
	}

	public void _PerformFullTransform( bool isOverwriteAfterIK )
	{
		if( _fileType == FileType.PMD ) {
			if( _boneData.pmdBoneType == PMDBoneType.UnderRotate && _inherenceParentBone != null ) {
				_FeedbackInherenceRotation( _inherenceParentBone.localRotation, isOverwriteAfterIK );
			} else if( _boneData.pmdBoneType == PMDBoneType.FollowRotate && _inherenceParentBone != null ) {
				float inherenceWeight = Mathf.Abs(_boneData.followCoef);
				Quaternion inherenceRotation = Quaternion.Slerp( Quaternion.identity, _inherenceParentBone.localRotation, inherenceWeight );
				if( inherenceWeight < 0.0f ) {
					inherenceRotation = MMD4MecanimCommon.Inverse( inherenceRotation );
				}
				_FeedbackInherenceRotation( inherenceRotation, isOverwriteAfterIK );
			}
		} else if( _fileType == FileType.PMX ) {
			if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceRotate) != PMXBoneFlags.None && _inherenceParentBone != null ) {
				Quaternion parentInherenceRotation = Quaternion.identity;
				if( (_boneData.pmxBoneFlags & PMXBoneFlags.InherenceLocal) != PMXBoneFlags.None ) {
					if( _rootBone != null ) {
						parentInherenceRotation = MMD4MecanimCommon.Inverse(_rootBone.transform.rotation) * _inherenceParentBone.rotation;
					} else {
						parentInherenceRotation = _inherenceParentBone.rotation;
					}
				} else {
					parentInherenceRotation = _inherenceParentBone.localRotation;
				}
				
				float inherenceWeight = _boneData.inherenceWeight;
				if( Mathf.Abs(inherenceWeight - 1.0f) > Mathf.Epsilon ) {
					float inherenceWeightAbs = Mathf.Abs(inherenceWeight);
					if( inherenceWeightAbs < 1.0f ) {
						parentInherenceRotation = Quaternion.Slerp( Quaternion.identity, parentInherenceRotation, inherenceWeightAbs );
					} else if( inherenceWeightAbs > 1.0f ) {
						float angle = Mathf.Acos(parentInherenceRotation.w) * 2.0f;
						float squared = 1.0f - parentInherenceRotation.w * parentInherenceRotation.w;
						Vector3 axis;
						if( squared < Mathf.Epsilon * 10.0f ) {
							axis = new Vector3(1.0f, 0.0f, 0.0f);
						} else {
							float s = 1.0f / Mathf.Sqrt(squared);
							axis = new Vector3(parentInherenceRotation.x, parentInherenceRotation.y, parentInherenceRotation.z) * s;
						}
						angle = angle * Mathf.Rad2Deg;
						parentInherenceRotation = Quaternion.AngleAxis( angle * inherenceWeightAbs, axis );
					}
					if( inherenceWeight < 0.0f ) {
						parentInherenceRotation = MMD4MecanimCommon.Inverse( parentInherenceRotation );
					}
				}
				
				_FeedbackInherenceRotation( parentInherenceRotation, isOverwriteAfterIK );
			}
		}
	}

	void _FeedbackInherenceRotation( Quaternion inherenceRotation, bool isOverwriteAfterIK )
	{
		if( _boneData == null ) {
			return;
		}

		if( isOverwriteAfterIK ) {
			_SetLocalRotation( MMD4MecanimCommon.FastMul( _userRotation, inherenceRotation ) );
		} else {
			_ApplyLocalRotation( inherenceRotation );
		}
		_parentUpdatedTransform = _GetParentUpdatedTransform();
		++_updatedTransform;
	}

	//--------------------------------------------------------------------------------------------------------------------------------------------------------------------

	Quaternion _ComputeLocalRotation()
	{
		if( _isModifiedHierarchy ) {
			return MMD4MecanimCommon.Inverse( _originalParentBone.gameObject.transform.rotation ) * this.gameObject.transform.rotation;
		} else {
			return this.gameObject.transform.localRotation;
		}
	}

	void _SetLocalRotation( Quaternion localRotation )
	{
		if( _isModifiedHierarchy ) {
			// Faster than _ApplyLocalRotation
			_originalLocalRotation = localRotation;
			Vector3 position = _originalParentBone.gameObject.transform.TransformPoint( _originalLocalPosition ); // Optimized: Fixed value.(Not compute)
			Quaternion rotation = _originalParentBone.gameObject.transform.rotation * _originalLocalRotation;
			this.gameObject.transform.position = position;
			this.gameObject.transform.rotation = rotation;
		} else {
			this.gameObject.transform.localRotation = localRotation;
		}
	}

	void _ApplyLocalRotation( Quaternion localRotation )
	{
		if( localRotation != Quaternion.identity ) {
			if( _isModifiedHierarchy ) {
				_originalLocalRotation *= localRotation;
				Vector3 position = _originalParentBone.gameObject.transform.TransformPoint( _originalLocalPosition ); // Optimized: Fixed value.(Not compute)
				Quaternion rotation = _originalParentBone.gameObject.transform.rotation * _originalLocalRotation;
				this.gameObject.transform.position = position;
				this.gameObject.transform.rotation = rotation;
			} else {
				this.gameObject.transform.localRotation *= localRotation;
			}
		}
	}
}
