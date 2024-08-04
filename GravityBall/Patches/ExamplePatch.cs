using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GravityBall.Patches
{
    /// <summary>
    /// This is an example patch, made to demonstrate how to use Harmony. You should remove it if it is not used.
    /// </summary>
    [HarmonyPatch(typeof(GameObject), "CreatePrimitive", MethodType.Normal)]
    public class GameObjectPatch
    {
        public static void Postfix(GameObject __result)
        {
            if (__result.GetComponent<Renderer>() != null)
            {
                __result.GetComponent<Renderer>().material.shader = Shader.Find("GorillaTag/UberShader");
                __result.GetComponent<Renderer>().material.color = Color.black;
            }
        }
    }
}
