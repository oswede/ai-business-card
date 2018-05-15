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

public partial class MMD4MecanimModel
{
	void _UpdateBone()
	{
		_ResetTransform();
	}

	void _LateUpdateBone()
	{
		_MarkChangedExternal();
		_PrepareLateTransform();
		_PerformUserTransform();
		_PerformFullTransform();
		_SolveIK();
		_PerformFullTransformAfterIK();
		_UpdatePPHBones();
	}

	//--------------------------------------------------------------------------------------------------------------------------------------------

	bool _isGenericAnimation {
		get {
			if( _animator == null ) {
				return false;
			}

			AnimatorClipInfo[] animationInfos = _animator.GetCurrentAnimatorClipInfo(0);
			if( animationInfos == null || animationInfos.Length == 0 ) {
				return false;
			}
			return !_animator.isHuman;
		}
	}

	void _ResetTransform()
	{
		_boneInherenceEnabledCached = this.boneInherenceEnabled;
		_ikEnabledCached = this.ikEnabled;

		// Skip boneInherence if playing Generic animation.
		if( !this.boneInherenceEnabledGeneric ) {
			if( _isGenericAnimation ) {
				_boneInherenceEnabledCached = false;
			}
		}

		if( !_boneInherenceEnabledCached && !_ikEnabledCached ) {
			return;
		}
		
		if( this.boneList != null ) {
			for( int i = 0; i < this.boneList.Length; ++i ) {
				this.boneList[i].ClearFlags();
			}
		}
		
		// Pending: MarkRigidBodySimulated
		
		if( _ikEnabledCached ) {
			if( this.ikList != null ) {
				for( int i = 0; i < this.ikList.Length; ++i ) {
					if( this.ikList[i] != null ) {
						this.ikList[i].MarkIKDepended();
					}
				}
			}
		}
		
		if( _sortedBoneList != null ) {
			for( int i = 0; i < _sortedBoneList.Length; ++i ) {
				if( _sortedBoneList[i] != null ) {
					_sortedBoneList[i].MarkModelControl();
					_sortedBoneList[i].MarkOverwriteAfterIK();
				}
			}
			for( int i = 0; i < _sortedBoneList.Length; ++i ) {
				if( _sortedBoneList[i] != null ) {
					_sortedBoneList[i].ResetTransform();
				}
			}
			for( int i = 0; i < _sortedBoneList.Length; ++i ) {
				if( _sortedBoneList[i] != null ) {
					_sortedBoneList[i].ResetTransform2();
				}
			}
			_PostfixTransform();
			for( int i = 0; i < _sortedBoneList.Length; ++i ) {
				if( _sortedBoneList[i] != null ) {
					_sortedBoneList[i].StoreLocalTransform();
				}
			}
		}
	}

	// for modifiedHierarchy
	void _PostfixTransform()
	{
		if( _sortedBoneList != null ) {
			for(;;) {
				bool updatedAnything = false;
				for( int i = 0; i < _sortedBoneList.Length; ++i ) {
					if( _sortedBoneList[i] != null ) {
						updatedAnything |= _sortedBoneList[i].PostfixTransfrom();
					}
				}
				if( !updatedAnything ) {
					break;
				}
			}
		}
	}
	
	void _MarkChangedExternal()
	{
		if( !_boneInherenceEnabledCached && !_ikEnabledCached ) {
			return;
		}
		
		if( this._sortedBoneList != null ) {
			for( int i = 0; i < _sortedBoneList.Length; ++i ) {
				if( _sortedBoneList[i] != null ) {
					_sortedBoneList[i].MarkChangedExternal();
				}
			}
		}
	}

	void _PrepareLateTransform()
	{
		if( !_boneInherenceEnabledCached && !_ikEnabledCached ) {
			bool userTransformAnything = false;
			for( int i = 0; i < _sortedBoneList.Length && !userTransformAnything; ++i ) {
				if( _sortedBoneList[i] != null ) {
					userTransformAnything = _sortedBoneList[i].IsUserTransform();
				}
			}
			if( !userTransformAnything ) {
				return; // Optimized: No effects _PerformUserTransform()
			}
		}
		
		for( int i = 0; i < _sortedBoneList.Length; ++i ) {
			if( _sortedBoneList[i] != null ) {
				_sortedBoneList[i].PrepareLateTransform(); // for User, Inherence, IK
			}
		}
	}

	void _PerformUserTransform()
	{
		if( this._sortedBoneList != null ) {
			for( int i = 0; i < _sortedBoneList.Length; ++i ) {
				if( _sortedBoneList[i] != null ) {
					_sortedBoneList[i].PerformUserTransform();
				}
			}
			
			_PostfixTransform();
		}
	}
	
	void _PerformFullTransform()
	{
		if( !_boneInherenceEnabledCached && !_ikEnabledCached ) {
			return;
		}
		
		if( this._sortedBoneList != null ) {
			for( int i = 0; i < _sortedBoneList.Length; ++i ) {
				if( _sortedBoneList[i] != null ) {
					_sortedBoneList[i].PerformFullTransform();
				}
			}
			
			_PostfixTransform();
		}
	}

	void _SolveIK()
	{
		if( !_ikEnabledCached ) {
			return;
		}

		if( this._sortedBoneList != null ) {
			for( int i = 0; i < _sortedBoneList.Length; ++i ) {
				if( _sortedBoneList[i] != null ) {
					_sortedBoneList[i].PrepareIKTransform();
				}
			}
		}

		if( this.ikList != null ) {
			for( int i = 0; i < this.ikList.Length; ++i ) {
				if( this.ikList[i] != null ) {
					this.ikList[i].Solve();
				}
			}
		}
		
		_PostfixTransform();
	}

	void _PerformFullTransformAfterIK()
	{
		if( !_boneInherenceEnabledCached && !_ikEnabledCached ) {
			return;
		}
		
		if( this._sortedBoneList != null ) {
			for( int i = 0; i < _sortedBoneList.Length; ++i ) {
				if( _sortedBoneList[i] != null ) {
					_sortedBoneList[i].PerformFullTransformAfterIK();
				}
			}
			
			_PostfixTransform();

			for( int i = 0; i < _sortedBoneList.Length; ++i ) {
				if( _sortedBoneList[i] != null ) {
					_sortedBoneList[i].PostfixIKTransform();
				}
			}

			_PostfixTransform();
		}
	}

	//--------------------------------------------------------------------------------------------------------------------------------------------

	void _UpdatePPHBones()
	{
		if( !this.pphEnabled ) {
			return;
		}
		if( _pphBones == null ) {
			return;
		}
		if( _animator == null ) {
			return;
		}

		bool isNoAnimation = false;
		AnimatorClipInfo[] animationInfos = _animator.GetCurrentAnimatorClipInfo(0);
		if( animationInfos == null || animationInfos.Length == 0 ) {
			isNoAnimation = true;
			if( !this.pphEnabledNoAnimation ) {
				return; // No playing animation.
			}
		}
		
		float pphRate = 0.0f;
		if( isNoAnimation ) {
			pphRate = 1.0f; // pphEnabledNoAnimation
		} else {
			foreach( AnimatorClipInfo animationInfo in animationInfos ) {
				if( !animationInfo.clip.name.EndsWith( ".vmd" ) ) {
					pphRate += animationInfo.weight;
				}
			}
			if( pphRate <= Mathf.Epsilon ) {
				return;
			}
		}
		
		float pphShoulderFixRate = this.pphShoulderFixRate * pphRate;
		
		for( int i = 0; i < _pphBones.Count; ++i ) {
			if( _pphBones[i].pphType == PPHType.Shoulder && this.pphShoulderEnabled ) {
				_UpdatePPHBone( _pphBones[i], pphShoulderFixRate );
			}
		}
	}
	
	static void _UpdatePPHBone( PPHBone pphBone, float fixRate )
	{
		if( pphBone == null || pphBone.target == null ) {
			return;
		}
		if( fixRate <= Mathf.Epsilon ) {
			return;
		}
		Quaternion rotation = pphBone.target.transform.localRotation;
		if( Mathf.Abs(rotation.x) <= Mathf.Epsilon &&
		   Mathf.Abs(rotation.y) <= Mathf.Epsilon &&
		   Mathf.Abs(rotation.z) <= Mathf.Epsilon &&
		   Mathf.Abs(rotation.w - 1.0f) <= Mathf.Epsilon ) {
			return;
		}
		
		pphBone.SnapshotChildRotations();
		
		if( fixRate >= 1.0f - Mathf.Epsilon ) {
			pphBone.target.transform.localRotation = Quaternion.identity;
		} else {
			rotation = Quaternion.Slerp( rotation, Quaternion.identity, fixRate );
			pphBone.target.transform.localRotation = rotation;
		}
		
		pphBone.RestoreChildRotations();
	}
}
