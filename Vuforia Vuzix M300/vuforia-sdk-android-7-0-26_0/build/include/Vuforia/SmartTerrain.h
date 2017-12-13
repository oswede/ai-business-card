/*===============================================================================
Copyright (c) 2017 PTC Inc. All Rights Reserved.

Vuforia is a trademark of PTC Inc., registered in the United States and other 
countries.

@file 
    SmartTerrain.h

@brief
    Header file for SmartTerrain class.
===============================================================================*/

#ifndef _VUFORIA_SMARTTERRAIN_H_
#define _VUFORIA_SMARTTERRAIN_H_

#include <Vuforia/Tracker.h>
#include <Vuforia/Vectors.h>

namespace Vuforia
{

// Forward declarations
class HitTestResult;

/// SmartTerrain class
class VUFORIA_API SmartTerrain : public Tracker
{
public:

    enum HITTEST_HINT
    {
        HITTEST_HINT_NONE,              ///< no hint
        HITTEST_HINT_HORIZONTAL_PLANE,  ///< hit test is performed on a horizontal plane
        HITTEST_HINT_VERTICAL_PLANE     ///< hit test is performed on a vertical plane
    };

    /// Returns the tracker class' type
    static Type getClassType();

    /// Performs hit test
    /**
     *  This function will detect a plane along a ray that is defined by the point which is passed in. 
     *  Recommended usage is to perform hitTest, get the number of hitTestResults generated using getHitTestResultCount() and then access a specific result using getHitTestResult()
     *  Hit test results are owned by SmartTerrain. Each call to hitTest() destroys and
     *  rebuilds the internal list of HitTestResults.
     *
     * \param point Point in normalized image coordinate space (top left (0,0), bottom right (1,1))
     * \param deviceHeight Height of the device center above ground in meters.
     * \param hint Give the implementation a hint about the orientation of the plane in the scene.
     */
    virtual void hitTest(Vec2F point, float deviceHeight, HITTEST_HINT hint) = 0;

    /// Gets the number of HitTestResults resulting from the last hitTest() call
    virtual int getHitTestResultCount() const = 0;

    /// Returns a pointer to a HitTestResult instance.
    /**
     * \param idx The index of the result. Must be equal or larger than 0 and less than the number of results returned by getHitTestResult().
     *
     * \return The HitTestResult instance for the given index.
     *
     * NOTE: The returned HitTestResult pointer will be invalidated with the next call to 'hitTest'
     *       or with a call to deinitailze the SmartTerrain instance. Accessing the pointer after
     *       these calls results in undefined behavior.
     */
    virtual const HitTestResult* getHitTestResult(int idx) const = 0;
};

} // namespace Vuforia

#endif // _VUFORIA_SMARTTERRAIN_H_
