using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

internal static class SwampFogRegression
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static object recipient;
    private static Vector3 position;
    private static int calls, hazard = 1;
    private static float total;
    private static string kind;
    private static bool fogEnabled = true;
    internal static int Run(Assembly mod)
    {
        var coordinator = mod.GetType("dda.RunCoordinator");
        var active = coordinator.GetField("Active", F); var running = coordinator.GetField("InRun", F);
        var mapType = AccessTools.TypeByName("MapHandler"); var mapInstance = mapType.BaseType.GetField("_instance", F);
        var orbType = AccessTools.TypeByName("OrbFogHandler"); var orbInstance = orbType.BaseType.GetField("_instance", F);
        var charType = AccessTools.TypeByName("Character"); var local = charType.GetField("localCharacter", F);
        var fields = new[] { active, running, mapInstance, orbInstance, local };
        var saved = new object[fields.Length]; for (int i=0;i<fields.Length;i++) saved[i]=fields[i].GetValue(null);
        var root = new GameObject("swamp fog fixture"); root.SetActive(false);
        var world = new GameObject("Gloom"); // Only the real ambient field needs to be enabled.
        var patch = new Harmony("dda.continued.swamp-fog-fixture");
        var safeType = AccessTools.TypeByName("Peak.GloomSafeZone");
        var safeList = (IList)safeType.GetField("ALL_GLOOM_SAFE_ZONES", F).GetValue(null);
        int oldSafeCount=safeList.Count, checks=0;
        Color oldDeep=Shader.GetGlobalColor("DeepFogColor");
        var helper=mod.GetType("dda.SwampChasingFog"); var reset=helper.GetMethod("Reset", F);
        var tintReset=mod.GetType("dda.ChasingFogTint").GetMethod("Reset", F);
        try
        {
            var map = root.AddComponent(mapType); mapInstance.SetValue(null,map);
            var segmentType=mapType.GetNestedType("MapSegment"); var segments=Array.CreateInstance(segmentType,5);
            for(int i=0;i<5;i++) segments.SetValue(Activator.CreateInstance(segmentType),i);
            Set(map,"segments",segments); Set(map,"currentSegment",3);
            var swampSegment=Child(world,"Swamp_Segment",new Vector3(1.25f,645.15f,1202));
            var exit=Child(world,"Campfire-Temple",new Vector3(6.832f,805.9f,1978.57f));
            var biome=segmentType.GetField("_biome",F); biome.SetValue(segments.GetValue(3),Enum.ToObject(biome.FieldType,8));
            Set(segments.GetValue(3),"_segmentParent",swampSegment); Set(segments.GetValue(3),"_segmentCampfire",exit);
            var gloomType=AccessTools.TypeByName("Peak.StatusFieldGloom");
            var fieldRoot=Child(world,"SleepyFog",new Vector3(-2.424f,784,1608)); var field=fieldRoot.AddComponent(gloomType);
            var statusField=AccessTools.TypeByName("Peak.StatusFieldBase"); var statusType=AccessTools.TypeByName("CharacterAfflictions+STATUSTYPE");
            Set(field,"statusType",Enum.Parse(statusType,"Drowsy")); Set(field,"statusAmountPerSecond",.009f);
            Set(field,"delayBeforeStatusOverTime",0f); Set(field,"lastEnteredTime",-999f);
            Set(field,"size",new Vector3(696.9f,46.87f,700));
            Set(field,"bounds",new Bounds(fieldRoot.transform.position-Vector3.up*46.87f/2,new Vector3(696.9f,46.87f,700)));
            var extras=statusField.GetField("additionalStatuses",F); extras.SetValue(field,Activator.CreateInstance(extras.FieldType));
            // A second Gloom field belongs to Citadel. It must not be selected.
            var temple=Child(world,"Temple_Segment",new Vector3(-17,862,2147)); temple.SetActive(false);
            Set(segments.GetValue(4),"_segmentParent",temple);
            var rising=temple.AddComponent(AccessTools.TypeByName("LavaRising"));
            var other=Child(temple,"SleepyFog (1)",new Vector3(0,804,2108)).AddComponent(gloomType);
            Set(other,"statusType",Enum.Parse(statusType,"Drowsy")); Set(other,"statusAmountPerSecond",.3f);
            var viewType=AccessTools.TypeByName("Photon.Pun.PhotonView"); var view=root.AddComponent(viewType);
            Set(view,"<IsMine>k__BackingField",true);
            var character=root.AddComponent(charType); local.SetValue(null,character); Set(character,"view",view);
            var dataType=AccessTools.Field(charType,"data").FieldType; var data=root.AddComponent(dataType); Set(character,"data",data); Set(data,"character",character);
            var afflictions=AccessTools.TypeByName("CharacterAfflictions"); recipient=root.AddComponent(afflictions); Set(recipient,"character",character);
            Set(recipient,"currentStatuses",new float[20]); Set(recipient,"currentIncrementalStatuses",new float[20]); Set(recipient,"currentDecrementalStatuses",new float[20]);
            var refsField=AccessTools.Field(charType,"refs"); var refs=Activator.CreateInstance(refsField.FieldType); Set(refs,"afflictions",recipient); refsField.SetValue(character,refs);
            var orb=root.AddComponent(orbType); orbInstance.SetValue(null,orb); Set(orb,"speed",.4f); Set(orb,"maxWaitTime",1000f);
            var sphereType=AccessTools.TypeByName("FogSphere"); var renderer=root.AddComponent<MeshRenderer>(); var sphere=root.AddComponent(sphereType);
            Set(sphere,"rend",renderer); Set(orb,"sphere",sphere);
            var originType=AccessTools.TypeByName("FogSphereOrigin"); var origins=Array.CreateInstance(originType,4);
            for(int i=0;i<4;i++) { var origin=Child(root,"origin"+i,new Vector3(0,800,1960)).AddComponent(originType); origins.SetValue(origin,i);
                Set(origin,"size",10000f); Set(origin,"moveOnHeight",10000f); Set(origin,"moveOnForward",10000f); Set(origin,"disableFog",i==3); }
            Set(orb,"origins",origins);
            var settings=AccessTools.TypeByName("RunSettings");
            patch.Patch(AccessTools.Method(settings,"GetValue",new[]{settings.GetNestedType("SETTINGTYPE"),typeof(bool)}),prefix:new HarmonyMethod(typeof(SwampFogRegression),"Hazard"));
            patch.Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("Ascents"),"fogEnabled"),prefix:new HarmonyMethod(typeof(SwampFogRegression),"FogOn"));
            patch.Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("Photon.Pun.PhotonNetwork"),"IsMasterClient"),prefix:new HarmonyMethod(typeof(SwampFogRegression),"NotMaster"));
            patch.Patch(AccessTools.PropertyGetter(charType,"Center"),prefix:new HarmonyMethod(typeof(SwampFogRegression),"Point"));
            patch.Patch(AccessTools.PropertyGetter(charType,"Head"),prefix:new HarmonyMethod(typeof(SwampFogRegression),"Point"));
            patch.Patch(AccessTools.Method(afflictions,"AddStatus"),prefix:new HarmonyMethod(typeof(SwampFogRegression),"Capture"){priority=Priority.Last});
            patch.Patch(AccessTools.Method(orbType,"WaitToMove"),prefix:new HarmonyMethod(typeof(SwampFogRegression),"SkipWait"));
            var initialize=orbType.GetMethod("SetFogOrigin",F); var update=orbType.GetMethod("Update",F);
            var timeToMove=orbType.GetMethod("TimeToMove",F); var sync=orbType.GetMethod("RPCA_SyncFog",F);
            var fogTick=sphereType.GetMethod("SetSharderVars",F); var gloomTick=gloomType.GetMethod("Update",F);
            var properties=new MaterialPropertyBlock(); var originalTint=new Color(.11f,.22f,.33f,.44f);
            properties.SetColor("_FogtintColor",originalTint); properties.SetFloat("FixtureUnrelated",123);
            properties.SetFloat("_DistanceVisibilityOffset",17); properties.SetFloat("_DistanceVisibilitysoftness",23);
            properties.SetFloat("_DistanceMaskNoise",15.4f); renderer.SetPropertyBlock(properties);
            var nativePurple=new Color(.36f,.22f,.43f,1); Shader.SetGlobalColor("DeepFogColor",nativePurple);
            running.SetValue(null,true); reset.Invoke(null,null);
            foreach(var selection in new[]{(17,0),(18,0),(19,0),(20,0),(0,1<<9),(0,1<<11)})
            {
                Select(selection.Item1,selection.Item2); initialize.Invoke(orb,new object[]{3});
                bool enabled=selection.Item1>=18 || selection.Item2==(1<<9);
                Assert(enabled ? (float)Get(orb,"currentSize")>800 && (float)Get(orb,"currentSize")<900 : (float)Get(orb,"currentSize")==10000,
                    "only tier 18 enables swamp geometry built from its field and campfire");
                if(!enabled) continue;
                Assert((Vector3)Get(sphere,"fogPoint")==exit.transform.position,"fog ends at actual swamp exit campfire");
                Set(orb,"currentWaitTime",20f); Assert(!(bool)timeToMove.Invoke(orb,null),"new swamp chase waits strictly beyond twenty seconds");
                Set(orb,"currentWaitTime",20.01f); Assert((bool)timeToMove.Invoke(orb,null),"new swamp chase starts after wait");
                Set(orb,"isMoving",true); float before=(float)Get(orb,"currentSize"); update.Invoke(orb,null);
                Assert(Math.Abs((float)Get(orb,"currentSize")-(before-.8f*Time.deltaTime))<.001f && (float)Get(orb,"speed")==.4f,"native moving radius uses x2 without stored multiplier");
                Assert((bool)Get(origins.GetValue(3),"disableFog"),"temporary enable flag restored after native update");
                Set(sphere,"ENABLE",1f); Set(sphere,"currentSize",50f); position=new Vector3(0,760,1500);
                float multiplier=selection.Item1==18 || selection.Item1==19 ? 1.3f : 1f;
                calls=0;total=0;fogTick.Invoke(sphere,null);
                Assert(calls==1 && kind=="Drowsy" && Close(total,.018f*multiplier*Time.deltaTime),"caught fog adds exactly two ambient rates, once through existing tier scaling");
                gloomTick.Invoke(field,null);
                Assert(calls==2 && Close(total,.027f*multiplier*Time.deltaTime),"ambient plus chasing fog equals three native rates; tier 20 removes tier 17 multiplier");
                renderer.GetPropertyBlock(properties); nativePurple.a=0;
                Assert(properties.GetColor("_FogtintColor")==nativePurple && properties.GetFloat("FixtureUnrelated")==123,"swamp uses native DeepFogColor without losing unrelated rendering properties");
                Assert(ExtendedVisibility(),"swamp renders with a 110-150 m fade while preserving native edge noise");
                position=exit.transform.position; calls=0; fogTick.Invoke(sphere,null); Assert(calls==0,"not overtaken means no extra sleep");
            }
            Select(20); position=new Vector3(0,760,1500); initialize.Invoke(orb,new object[]{3}); Set(sphere,"currentSize",50f); Set(sphere,"ENABLE",1f);
            for(int i=0;i<100;i++) { calls=0;total=0;fogTick.Invoke(sphere,null);Assert(calls==1&&Close(total,.018f*Time.deltaTime),"repeated updates keep rate fixed"); }
            hazard=0;calls=0;fogTick.Invoke(sphere,null);gloomTick.Invoke(field,null);Assert(calls==0,"sleep hazard switch blocks ambient and chasing status");hazard=1;
            var candle=Child(root,"safe candle",position).AddComponent(safeType); Set(candle,"_isLit",true);Set(candle,"statusProtectionRadius",10f);safeList.Add(candle);
            calls=0;fogTick.Invoke(sphere,null);gloomTick.Invoke(field,null);Assert(calls==0,"lit native safe zone protects against both sleep sources");Set(candle,"_isLit",false);
            calls=0;fogTick.Invoke(sphere,null);Assert(calls==1,"extinguished candle no longer protects");
            Set(data,"_isSkeleton",true);calls=0;total=0;fogTick.Invoke(sphere,null);
            Assert(kind=="Drowsy" && calls==1 && Close(total,.018f*.25f*Time.deltaTime),"skeleton receives existing quarter drowsy rule rather than injury times 99");Set(data,"_isSkeleton",false);
            // Native late-join/reconnect setter may arrive before map selection.
            Set(map,"currentSegment",0); initialize.Invoke(orb,new object[]{3}); sync.Invoke(orb,new object[]{456f,true});
            Set(map,"currentSegment",3);update.Invoke(orb,null);
            Assert((Vector3)Get(sphere,"fogPoint")==exit.transform.position && Math.Abs((float)Get(orb,"currentSize")-(456f-.8f*Time.deltaTime))<.001f,"delayed map initialization preserves received host phase");
            update.Invoke(orb,null);Assert((float)Get(orb,"currentSize")<456,"repeated initialization does not rewind shared phase");
            Set(map,"currentSegment",0);initialize.Invoke(orb,new object[]{3});Set(map,"currentSegment",3);Set(orb,"isMoving",false);update.Invoke(orb,null);
            Assert((float)Get(orb,"currentSize")<900,"old save with disabled native radius gets a fresh swamp origin once map is ready");
            // Volcano colour is the exact original author's value and restores afterward.
            Select(14);biome.SetValue(segments.GetValue(3),Enum.ToObject(biome.FieldType,3));Set(sphere,"ENABLE",0f);fogTick.Invoke(sphere,null);renderer.GetPropertyBlock(properties);
            Assert(properties.GetColor("_FogtintColor")==new Color(.3f,0,0,0),"Volcano dark red restored");
            Assert(ExtendedVisibility(),"Volcano shares the 150 m visual range without changing radius");
            float radiusBeforeVisualUpdates=(float)Get(sphere,"currentSize");
            for(int i=0;i<100;i++) fogTick.Invoke(sphere,null);
            renderer.GetPropertyBlock(properties);
            Assert(ExtendedVisibility() && properties.GetFloat("_FogDepth")==radiusBeforeVisualUpdates,"repeated visual updates preserve range and native damage radius");
            Select(20);biome.SetValue(segments.GetValue(3),Enum.ToObject(biome.FieldType,1));fogTick.Invoke(sphere,null);renderer.GetPropertyBlock(properties);
            Assert(OriginalVisibility(),"leaving Volcano for another biome restores its original distance overrides even at tier 20");
            biome.SetValue(segments.GetValue(3),Enum.ToObject(biome.FieldType,3));fogTick.Invoke(sphere,null);
            Select(0);fogTick.Invoke(sphere,null);renderer.GetPropertyBlock(properties);
            Assert(properties.GetColor("_FogtintColor")==originalTint && properties.GetFloat("FixtureUnrelated")==123,"official mode restores full original property block");
            Assert(OriginalVisibility(),"official mode restores prior distance overrides");
            properties.Clear();properties.SetFloat("FixtureUnrelated",321);renderer.SetPropertyBlock(properties);Select(14);fogTick.Invoke(sphere,null);tintReset.Invoke(null,null);renderer.GetPropertyBlock(properties);
            Assert(!properties.HasColor("_FogtintColor") && properties.GetFloat("FixtureUnrelated")==321,"reset restores originally absent tint without adding a permanent material override");
            Assert(!properties.HasFloat("_DistanceVisibilityOffset") && !properties.HasFloat("_DistanceVisibilitysoftness"),"reset removes distance overrides when the original block had none");
            Select(20);biome.SetValue(segments.GetValue(3),Enum.ToObject(biome.FieldType,8));Set(map,"currentSegment",4);biome.SetValue(segments.GetValue(4),Enum.ToObject(biome.FieldType,8));initialize.Invoke(orb,new object[]{4});
            Assert((bool)Get(orb,"hasArrived") && !root.activeSelf,"Citadel checkpoint uses native chasing-fog shutdown");
            fogTick.Invoke(sphere,null);renderer.GetPropertyBlock(properties);
            Assert(!properties.HasFloat("_DistanceVisibilityOffset") && !properties.HasFloat("_DistanceVisibilitysoftness"),"Citadel with a Swamp biome tag retains its own native rendering distance");
            return checks;
            void Select(int level,int mask=0)=>active.SetValue(null,Activator.CreateInstance(mod.GetType("dda.DifficultySnapshot"),level,mask,true,true));
            bool ExtendedVisibility()=>properties.GetFloat("_DistanceVisibilityOffset")==110f && properties.GetFloat("_DistanceVisibilitysoftness")==40f && properties.GetFloat("_DistanceMaskNoise")==15.4f;
            bool OriginalVisibility()=>properties.GetFloat("_DistanceVisibilityOffset")==17f && properties.GetFloat("_DistanceVisibilitysoftness")==23f;
        }
        finally
        {
            tintReset.Invoke(null,null);reset.Invoke(null,null);patch.UnpatchSelf();
            while(safeList.Count>oldSafeCount)safeList.RemoveAt(safeList.Count-1);
            for(int i=0;i<fields.Length;i++)fields[i].SetValue(null,saved[i]);
            Shader.SetGlobalColor("DeepFogColor",oldDeep);recipient=null;hazard=1;fogEnabled=true;
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(world);
        }
        void Assert(bool pass,string reason){if(!pass)throw new Exception(reason+"; calls="+calls+" total="+total+" kind="+kind);checks++;}
    }
    private static bool Close(float a,float b)=>Math.Abs(a-b)<1e-7f;
    private static GameObject Child(GameObject root,string name,Vector3 position){var go=new GameObject(name);go.transform.SetParent(root.transform);go.transform.position=position;return go;}
    private static object Get(object value,string field)=>AccessTools.Field(value.GetType(),field).GetValue(value);
    private static void Set(object value,string field,object data)=>AccessTools.Field(value.GetType(),field).SetValue(value,data);
    private static bool Point(ref Vector3 __result){__result=position;return false;}
    private static bool FogOn(ref bool __result){__result=fogEnabled;return false;}
    private static bool NotMaster(ref bool __result){__result=false;return false;}
    private static bool SkipWait()=>false;
    private static bool Hazard(object setting,ref int __result){if(setting.ToString()!="Hazard_SleepyGloom")return true;__result=hazard;return false;}
    private static bool Capture(object __instance,object statusType,float amount){if(!ReferenceEquals(__instance,recipient))return true;calls++;total+=amount;kind=statusType.ToString();return false;}
}
