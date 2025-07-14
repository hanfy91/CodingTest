using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MoveType
{
    Linner,
    EasyIn,
    EasyOut,
    EasyInOut,
    Custom=99,
}
public class Mover : MonoBehaviour
{
    public MoveType moveType;
    public AnimationCurve animationCurve;
    public bool playOnAwake = false;
    public bool pingpong = false;
    public float moveTime = 1f;
    public Vector3 beginPosition;
    public Vector3 endPosition;
    private Coroutine moveCoroutine;
    
    // Start is called before the first frame update
    void Awake()
    {
        if (playOnAwake)
        {
            Move(gameObject, beginPosition, endPosition, moveTime, pingpong);
        }
        else
        {
            // Set the initial position to the begin position
            transform.position = beginPosition;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    //TODO change to static extension method and static controller
    void Move(GameObject gameObject, Vector3 begin, Vector3 end, float time, bool pingpong)
    {
        if (gameObject == null) return;
        if (time <= 0) return;
        
        if (moveCoroutine!=null)
        {
            StopCoroutine(moveCoroutine);
        }
        //TODO Cache the coroutine 
        moveCoroutine = StartCoroutine(MoveCoroutine(gameObject, begin, end, time, pingpong));
    }
    private IEnumerator MoveCoroutine(GameObject o, Vector3 begin, Vector3 end, float time, bool b)
    {
        do
        {
            yield return MoveOneWay(o, begin, end, time);
            if (b)
            {
                yield return MoveOneWay(o, end, begin, time );
            }
            else
            {
                yield break;
            }
        }
        while (pingpong);
        moveCoroutine = null;
    }
    private IEnumerator MoveOneWay(GameObject o, Vector3 begin, Vector3 end, float time)
    {
        float t = 0;
        while (t < time)
        {
            t += Time.deltaTime;
            float percent = Mathf.Clamp01(t / time);
            float movePercent = 0;
            if (moveType != MoveType.Custom)
            {
                movePercent = MoveUtils.MoveStrategies[moveType](percent);
            }
            else
            {
                movePercent = animationCurve != null ? animationCurve.Evaluate(percent) : percent;
            }
            o.transform.position = Vector3.Lerp(begin, end, movePercent);
            yield return null;
        }
        o.transform.position = end;
    }
}

public static class MoveUtils
{
    public static Dictionary<MoveType,Func<float,float>> MoveStrategies = new Dictionary<MoveType, Func<float,float>>()
    {
        { MoveType.Linner, Linner },
        { MoveType.EasyIn, EasyIn },
        { MoveType.EasyOut, EasyOut },
        { MoveType.EasyInOut, EasyInOut },
    };
    public static float Linner(float t)
    {
        return t;
    }
    public static float EasyIn(float t)
    {
        return t * t;
    }
    public static float EasyOut(float t)
    {
        return 1 - (1 - t) * (1 - t);
    }
    public static float EasyInOut(float t)
    {
        if (t < 0.5f)
        {
            return 2 * t * t;
        }
        else
        {
            return 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
        }
    }
}
