//#define MMD4MECANIM_KEEPIKTARGETBONE

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
using IKData			= MMD4MecanimData.IKData;
using IKLinkData		= MMD4MecanimData.IKLinkData;
using FileType			= MMD4MecanimData.FileType;

using Bone				= MMD4MecanimBone;

public partial class MMD4MecanimModel
{
	public class IK
	{
		public enum IKAxis
		{
			None,
			X,
			Y,
			Z,
			Free,
		}

		MMD4MecanimModel	_model;
		FileType			_fileType;
		int					_ikID;
		IKData				_ikData;

		public int ikID { get { return _ikID; } }
		public IKData ikData { get { return _ikData; } }

		public class IKLink
		{
			public IKLinkData	ikLinkData;
			public IKAxis		ikAxis;
			public Bone			bone;
		}

		Bone				_destBone;
		Bone				_targetBone;
		IKLink[]			_ikLinkList;

		public Bone destBone { get { return _destBone; } }
		public Bone targetBone { get { return _targetBone; } }
		public IKLink[] ikLinkList { get { return _ikLinkList; } }

		public bool ikEnabled {
			get {
				if(_destBone != null ) {
					return _destBone.ikEnabled;
				}
				return false;
			}
			set {
				if( _destBone != null ) {
					_destBone.ikEnabled = value;
				}
			}
		}

		public float ikWeight {
			get {
				if(_destBone != null ) {
					return _destBone.ikWeight;
				}
				return 0.0f;
			}
			set {
				if( _destBone != null ) {
					_destBone.ikWeight = value;
				}
			}
		}

		public GameObject ikGoal {
			get {
				if(_destBone != null ) {
					return _destBone.ikGoal;
				}
				return null;
			}
			set {
				if( _destBone != null ) {
					_destBone.ikGoal = value;
				}
			}
		}
		
		public IK( MMD4MecanimModel model, int ikID )
		{
			if( model == null || model.modelData == null || model.modelData.ikDataList == null ||
			    ikID >= model.modelData.ikDataList.Length ) {
				Debug.LogError("");
				return;
			}
			
			_model	= model;
			_ikID	= ikID;
			_ikData	= model.modelData.ikDataList[ikID];
			if( _model.modelData != null ) {
				_fileType = _model.modelData.fileType;
			}

			if( _ikData != null ) {
				_destBone = model.GetBone( _ikData.destBoneID );
				_targetBone = model.GetBone( _ikData.targetBoneID );
				if( _ikData.ikLinkDataList != null ) {
					_ikLinkList = new IKLink[_ikData.ikLinkDataList.Length];
					for( int i = 0; i < _ikData.ikLinkDataList.Length; ++i ) {
						_ikLinkList[i] = new IKLink();
						_ikLinkList[i].ikLinkData = _ikData.ikLinkDataList[i];
						if( _ikLinkList[i].ikLinkData != null ) {
							_ikLinkList[i].bone = model.GetBone( _ikLinkList[i].ikLinkData.ikLinkBoneID );
							Vector3 lowerLimit = _ikLinkList[i].ikLinkData.lowerLimitAsDegree;
							Vector3 upperLimit = _ikLinkList[i].ikLinkData.upperLimitAsDegree;
							if( MMD4MecanimCommon.FuzzyZero(lowerLimit[1]) && MMD4MecanimCommon.FuzzyZero(upperLimit[1]) &&
							    MMD4MecanimCommon.FuzzyZero(lowerLimit[2]) && MMD4MecanimCommon.FuzzyZero(upperLimit[2]) ) {
								_ikLinkList[i].ikAxis = IKAxis.X;
							} else if( MMD4MecanimCommon.FuzzyZero(lowerLimit[0]) && MMD4MecanimCommon.FuzzyZero(upperLimit[0]) &&
							           MMD4MecanimCommon.FuzzyZero(lowerLimit[2]) && MMD4MecanimCommon.FuzzyZero(upperLimit[2]) ) {
								_ikLinkList[i].ikAxis = IKAxis.Y;
							} else if( MMD4MecanimCommon.FuzzyZero(lowerLimit[0]) && MMD4MecanimCommon.FuzzyZero(upperLimit[0]) &&
							           MMD4MecanimCommon.FuzzyZero(lowerLimit[1]) && MMD4MecanimCommon.FuzzyZero(upperLimit[1]) ) {
								_ikLinkList[i].ikAxis = IKAxis.Z;
							} else {
								_ikLinkList[i].ikAxis = IKAxis.Free;
							}
						}
					}
				}
			}
		}

		bool _Precheck()
		{
			if( _model == null || _ikData == null || _ikLinkList == null ) {
				return false;
			}
			if( _destBone == null || _destBone.gameObject == null || _targetBone == null || _targetBone.gameObject == null ) {
				return false;
			}
			if( _destBone.isRigidBodySimulated ) {
				return false;
			}
			
			for( int i = 0; i < _ikLinkList.Length; ++i ) {
				if( _ikLinkList[i].ikLinkData == null ||
				   _ikLinkList[i].bone == null ||
				   _ikLinkList[i].bone.boneData == null ||
				   _ikLinkList[i].bone.gameObject == null ) {
					return false;
				}
				if( _ikLinkList[i].bone.isRigidBodySimulated ) {
					return false;
				}
			}
			
			return true;
		}

		public void MarkIKDepended()
		{
			if( !_Precheck() || !this.ikEnabled ) {
				return;
			}

			float ikWeight = this.ikWeight;

			if( _destBone != null ) {
				_destBone.isIKDepended = true;
				_destBone.feedbackIKWeight = Mathf.Max(_destBone.feedbackIKWeight, ikWeight);
			}
			if( _targetBone != null ) {
				_targetBone.isIKDepended = true;
				_targetBone.feedbackIKWeight = Mathf.Max(_targetBone.feedbackIKWeight, ikWeight);
			}
			if( _ikLinkList != null ) {
				for( int i = 0; i < _ikLinkList.Length; ++i ) {
					if( _ikLinkList[i].bone != null ) {
						_ikLinkList[i].bone.isIKDepended = true;
						_ikLinkList[i].bone.feedbackIKWeight = Mathf.Max(_ikLinkList[i].bone.feedbackIKWeight, ikWeight);
					}
				}
			}
		}

		public void Destroy()
		{
			_model = null;
			_ikData = null;
			_destBone = null;
			_targetBone = null;
		}

		public void Solve()
		{
			if( _destBone == null || !_destBone.ikEnabled ) {
				return;
			}
			if( _destBone.ikGoal != null ) {
				_destBone.transform.position = _destBone.ikGoal.transform.position;
			}

			if( _fileType == FileType.PMD ) {
				SolvePMD();
			} else if( _fileType == FileType.PMX ) {
				SolvePMX();
			}
		}

		public void SolvePMD()
		{
			if( !_Precheck() ) {
				return;
			}

			#if MMD4MECANIM_KEEPIKTARGETBONE
			Quaternion targetRotation = _targetBone.gameObject.transform.localRotation;
			#endif
			
			Vector3 destPos = _destBone.gameObject.transform.position;
			float angleConstraint = _ikData.angleConstraint;
			int ikLinkListLength = _ikLinkList.Length;
			int iteration = _ikData.iteration;
			for( int ite = 0; ite < iteration; ++ite ) {
				for( int i = 0; i < ikLinkListLength; ++i ) {
					Vector3 targetPos = _targetBone.gameObject.transform.position;
					
					Vector3 localDestVec = destPos;
					Vector3 localTargetVec = targetPos;
					
					{
						Matrix4x4 inverseTransform = _ikLinkList[i].bone.originalWorldToLocalMatrix;
						localDestVec = inverseTransform.MultiplyPoint3x4( localDestVec );
						localTargetVec = inverseTransform.MultiplyPoint3x4( localTargetVec );
						localDestVec.Normalize();
						localTargetVec.Normalize();
						Vector3 tempVec = localDestVec - localTargetVec;
						if( Vector3.Dot( tempVec, tempVec ) < 1e-09f ) {
							break;
						}
					}
					
					Vector3 axis = Vector3.Cross( localTargetVec, localDestVec );
					if( _ikLinkList[i].bone.boneData.isLimitAngleX ) {
						if( axis.x >= 0.0f ) {
							axis.Set( 1.0f, 0.0f, 0.0f );
						} else {
							axis.Set( -1.0f, 0.0f, 0.0f );
						}
					} else {
						axis.Normalize();
					}
					
					float dot = Vector3.Dot( localTargetVec, localDestVec );
					dot = Mathf.Clamp( dot, -1.0f, 1.0f );
					
					float rx = Mathf.Acos(dot) * 0.5f;
					rx = Mathf.Min( rx, angleConstraint * (float)((i + 1) * 2) );

					float rs = Mathf.Sin( rx );
					
					Quaternion q = Quaternion.identity;
					q.x = axis.x * rs;
					q.y = axis.y * rs;
					q.z = axis.z * rs;
					q.w = Mathf.Cos( rx );
					
					if( _ikLinkList[i].bone.boneData.isLimitAngleX ) {
						bool inverseAngle = (ite == 0);
						Vector3 eulerAngles = Vector3.zero;
						if( ite == 0 ) {
							q = _ikLinkList[i].bone.originalLocalRotation * q;
							eulerAngles = q.eulerAngles;
						} else { // Fix for Unity.(Unstable eulerAngles near 90.)
							eulerAngles = q.eulerAngles;
							eulerAngles.x = MMD4MecanimCommon.NormalizeAsDegree( eulerAngles.x ) + _ikLinkList[i].bone.ikEulerAngles.x;
						}
						eulerAngles.x = _ClampEuler( eulerAngles.x, 0.5f, 180.0f, inverseAngle );
						eulerAngles.y = 0.0f;
						eulerAngles.z = 0.0f;
						_ikLinkList[i].bone.ikEulerAngles = eulerAngles;
						_ikLinkList[i].bone.SetLocalRotationFromIK( Quaternion.Euler( eulerAngles ) );
					} else {
						_ikLinkList[i].bone.ApplyIKRotation( q );
						_IKMuscle( _ikLinkList[i].bone );
					}
				}
			}
			
			#if MMD4MECANIM_KEEPIKTARGETBONE
			_targetBone.gameObject.transform.localRotation = targetRotation;
			#endif
		}

		static float _ClampEuler(float r, float lower, float upper, bool inverse)
		{
			if( r < lower ) {
				if( inverse ) {
					float inv = lower * 2.0f - r;
					if( inv <= upper ) {
						return inv;
					}
				}
				return lower;
			} else if( r > upper ) {
				if( inverse ) {
					float inv = upper * 2.0f - r;
					if( inv >= lower ) {
						return inv;
					}
				}
				
				return upper;
			}
			
			return r;
		}

		void _IKMuscle( Bone bone )
		{
		}

		public void SolvePMX()
		{
			if( !_Precheck() ) {
				return;
			}

			#if MMD4MECANIM_KEEPIKTARGETBONE
			Quaternion targetRotation = _targetBone.gameObject.transform.localRotation;
			#endif

			Vector3 destPos = _destBone.gameObject.transform.position;
			float angleConstraint = _ikData.angleConstraint;
			int ikLinkListLength = _ikLinkList.Length;
			int iteration = _ikData.iteration;

			for( int ite = 0; ite < iteration; ++ite ) {
				for( int i = 0; i < ikLinkListLength; ++i ) {
					Vector3 targetPos = _targetBone.gameObject.transform.position;

					Vector3 localDestVec = destPos;
					Vector3 localTargetVec = targetPos;

					{
						Matrix4x4 inverseTransform = _ikLinkList[i].bone.originalWorldToLocalMatrix;
						localDestVec = inverseTransform.MultiplyPoint3x4( localDestVec );
						localTargetVec = inverseTransform.MultiplyPoint3x4( localTargetVec );
						localDestVec.Normalize();
						localTargetVec.Normalize();
						Vector3 tempVec = localDestVec - localTargetVec;
						if( Vector3.Dot( tempVec, tempVec ) < 1e-09f ) {
							break;
						}
					}

					Vector3 axis = Vector3.Cross( localTargetVec, localDestVec );
					if( _ikLinkList[i].ikLinkData.hasAngleJoint ) {
						if( _ikLinkList[i].ikAxis == IKAxis.X ) {
							// X Limit
							if( axis.x >= 0.0f ) {
								axis.Set( 1.0f, 0.0f, 0.0f );
							} else {
								axis.Set( -1.0f, 0.0f, 0.0f );
							}
						} else if( _ikLinkList[i].ikAxis == IKAxis.Y ) {
							// Y Limit
							if( axis.y >= 0.0f ) {
								axis.Set( 0.0f, 1.0f, 0.0f );
							} else {
								axis.Set( 0.0f, -1.0f, 0.0f );
							}
						} else if(_ikLinkList[i].ikAxis == IKAxis.Z ) {
							// Z Limit
							if( axis.z >= 0.0f ) {
								axis.Set( 0.0f, 0.0f, 1.0f );
							} else {
								axis.Set( 0.0f, 0.0f, -1.0f );
							}
						} else {
							axis.Normalize();
						}
					} else {
						axis.Normalize();
					}

					float dot = Vector3.Dot( localTargetVec, localDestVec );
					dot = Mathf.Clamp( dot, -1.0f, 1.0f );

					float rx = Mathf.Acos(dot) * 0.5f;
					rx = Mathf.Min( rx, angleConstraint * (float)((i + 1) * 2) );
					
					float rs = Mathf.Sin( rx );

					Quaternion q = Quaternion.identity;
					q.x = axis.x * rs;
					q.y = axis.y * rs;
					q.z = axis.z * rs;
					q.w = Mathf.Cos( rx );

					bool inverseAngle = (ite == 0);
					if( _ikLinkList[i].ikLinkData.hasAngleJoint ) {
						Vector3 eulerAngles = Vector3.zero;
						if( ite == 0 ) {
							q = _ikLinkList[i].bone.originalLocalRotation * q;
							eulerAngles = MMD4MecanimCommon.NormalizeAsDegree( q.eulerAngles );
						} else { // Fix for Unity.(Unstable eulerAngles near 90.)
							eulerAngles = MMD4MecanimCommon.NormalizeAsDegree( q.eulerAngles );
							eulerAngles += _ikLinkList[i].bone.ikEulerAngles;
						}
						Vector3 lowerLimit = _ikLinkList[i].ikLinkData.lowerLimitAsDegree;
						Vector3 upperLimit = _ikLinkList[i].ikLinkData.upperLimitAsDegree;
						if( _ikLinkList[i].ikAxis == IKAxis.X ) {
							// X Limit
							eulerAngles.x = _ClampEuler( eulerAngles.x, lowerLimit[0], upperLimit[0], inverseAngle );
							eulerAngles.y = 0.0f;
							eulerAngles.z = 0.0f;
						} else if( _ikLinkList[i].ikAxis == IKAxis.Y ) {
							// Y Limit
							eulerAngles.x = 0.0f;
							eulerAngles.y = _ClampEuler( eulerAngles.y, lowerLimit[1], upperLimit[1], inverseAngle );
							eulerAngles.z = 0.0f;
						} else if( _ikLinkList[i].ikAxis == IKAxis.Z ) {
							// Z Limit
							eulerAngles.x = 0.0f;
							eulerAngles.y = 0.0f;
							eulerAngles.z = _ClampEuler( eulerAngles.z, lowerLimit[2], upperLimit[2], inverseAngle );
						} else {
							// Anti Gimbal Lock
							if( lowerLimit.x >= -180.0f && upperLimit.x <= 180.0f ) {
								eulerAngles[0] = Mathf.Clamp( eulerAngles[0], -176.0f, 176.0f );
							} else if( lowerLimit.y >= -180.0f && upperLimit.y <= 180.0f ) {
								eulerAngles[1] = Mathf.Clamp( eulerAngles[1], -176.0f, 176.0f );
							} else {
								eulerAngles[2] = Mathf.Clamp( eulerAngles[2], -176.0f, 176.0f );
							}
							eulerAngles.x = _ClampEuler( eulerAngles.x, lowerLimit[0], upperLimit[0], inverseAngle );
							eulerAngles.y = _ClampEuler( eulerAngles.y, lowerLimit[1], upperLimit[1], inverseAngle );
							eulerAngles.z = _ClampEuler( eulerAngles.z, lowerLimit[2], upperLimit[2], inverseAngle );
						}
						_ikLinkList[i].bone.ikEulerAngles = eulerAngles;
						_ikLinkList[i].bone.SetLocalRotationFromIK( Quaternion.Euler( eulerAngles ) );
					} else {
						_ikLinkList[i].bone.ApplyIKRotation( q );
						_IKMuscle( _ikLinkList[i].bone );
					}
				}
			}

			#if MMD4MECANIM_KEEPIKTARGETBONE
			_targetBone.gameObject.transform.localRotation = targetRotation;
			#endif
		}
	}
}
