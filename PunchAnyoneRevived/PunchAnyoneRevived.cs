using MSCLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using UnityEngine;

namespace PunchAnyoneRevived
{
    enum NpcType
    {
        Default,
        Ventti,
        Grandma,
        Drunk1,
        Lindell,
        Guard,
        Singer,
        Synthist,
        Jani,
        Petteri,
        SuskiPassenger,
        Strawberryman,
        BusDriver,
        ShitNPC,
        SigneTheatre,
        MarkkuTheatre,
        SoccerKid,
        PartsSalesman,
        Pena,
        TheatreCop,
        TheatreGuy,
        theatrePresident,
        Jokke,
        Uncle
    }

    public class PunchAnyoneRevived : Mod
    {
        public override string ID => "PunchAnyoneRevived"; // Your (unique) mod ID 
        public override string Name => "Punch Anyone Revived"; // Your mod name
        public override string Author => "Swooceman"; // Name of the Author (your name)
        public override string Version => "1.0"; // Version
        public override string Description => "A revival of the mod PunchAnyone by haverdaden!"; // Short description of your mod

        // Unity vars
        GameObject teimoInBikeRagdollBase;
        GameObject camera;
        GameObject cameraForState;
        GameObject crime;
        AudioClip punchSound;

        // Mod vars
        bool teimoPunched = false;
        List<PunchNPC> NPCs;
        Dictionary<string, GameObject> foundObjects;

        // Settings
        SettingsCheckBox becomeWanted;

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, Mod_OnLoad);
            SetupFunction(Setup.ModSettings, Mod_Settings);
        }

        private void Mod_Settings()
        {
            // All settings should be created here. 
            // DO NOT put anything that isn't settings or keybinds in here!
            this.becomeWanted = Settings.AddCheckBox("PunchAnyoneRevivedBecomeWanted", "Become wanted", true);
        }

        private void Mod_OnLoad()
        {
            // Called once, when mod is loading after game is fully loaded
            this.foundObjects = new Dictionary<string, GameObject>();
            this.teimoInBikeRagdollBase = this.GetGameObject("STORE/TeimoInBike/Bicycle/Functions/Teimo/RagDoll");
            //teimoMesh = this.GetGameObject("STORE/TeimoInBike/Bicycle/Functions/Teimo/RagDoll/bodymesh");
            this.camera = this.GetGameObject("PLAYER/Pivot/AnimPivot/Camera/FPSCamera/FPSCamera");
            this.cameraForState = this.GetGameObject("PLAYER/Pivot/AnimPivot/Camera/FPSCamera");
            this.crime = this.GetGameObject("Systems/PlayerWanted");

            //this.punchSound = MSCLoader.ModAudio.LoadAudioClipFromFile("Mods/Assets/PunchAnyoneRevived/punch.wav", false);
            this.punchSound = this.GetGameObject("MasterAudio/PlayerMisc/punch0").GetComponent<AudioSource>().clip;

            this.initAll();

            this.cameraForState.FsmInject("PlayerFunctions", "Fist", this.punchHandler);
        }

        private GameObject GetObjectByPath(string name)
        {
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            string[] pathParts = name.Split('/');
            foreach (GameObject obj in allObjects)
            {
                Transform parent = obj.transform;
                for (int i = pathParts.Length - 1; i >= 0; i--)
                {
                    if (parent != null && parent.gameObject.name == pathParts[i])
                    {
                        if (i == 0)
                        {
#if DEBUG
                            ModConsole.Print("Found " + name);
#endif
                            return obj.gameObject;
                        }
                        else
                        {
                            parent = parent.parent;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
#if DEBUG
            ModConsole.Print("Object '" + name + "' not found!");
#endif
            return null;
        }

        private GameObject GetGameObject(string name)
        {
            if (this.foundObjects.ContainsKey(name))
            {
                return this.foundObjects[name];
            }
            else
            {
                GameObject ret = this.GetObjectByPath(name);
                foundObjects.Add(name, ret);
                return ret;
            }
        }

        private void initNPC(GameObject hitObject, GameObject bodyMesh, GameObject npcBase, GameObject skeleton, Action punched, NpcType npcType = NpcType.Default)
        {
            if (hitObject == null || bodyMesh == null || npcBase == null || skeleton == null)
            {
                return;
            }
            else
            {
                BoxCollider hitbox = hitObject.GetComponent<BoxCollider>();
                if (hitbox == null)
                {
                    hitbox = hitObject.AddComponent<BoxCollider>();
                    //headHitbox.size = new Vector3(1.5f, 1.5f, 1.5f);
                    hitbox.isTrigger = true;
                }

                this.NPCs.Add(new PunchNPC()
                {
                    Head = hitObject,
                    Mesh = bodyMesh,
                    Body = npcBase,
                    Skeleton = skeleton,
                    NpcObjectType = npcType,
                    punched = punched
                });
            }
        }

        private bool punchNPC(PunchNPC npc, RaycastHit hit)
        {
            if (npc.Head == null || npc.Body == null || npc.Mesh == null || npc.Skeleton == null)
            {
                return false;
            }
            else
            {
                BoxCollider hitbox = npc.Head.GetComponent<BoxCollider>();
                if (hit.collider == hitbox)
                {
                    // Use Teimo's ragdoll as base.
                    GameObject teimoRagdoll = GameObject.Instantiate(teimoInBikeRagdollBase);
                    GameObject teimoMesh = teimoRagdoll.transform.Find("bodymesh").gameObject;
                    GameObject npcMesh = GameObject.Instantiate(npc.Mesh);

                    npcMesh.transform.SetParent(teimoMesh.transform.parent);
                    npcMesh.transform.localPosition = teimoMesh.transform.localPosition;
                    npcMesh.transform.localRotation = teimoMesh.transform.localRotation;
                    SkinnedMeshRenderer npcSkin = npcMesh.GetComponent<SkinnedMeshRenderer>();
                    SkinnedMeshRenderer teimoSkin = teimoMesh.GetComponent<SkinnedMeshRenderer>();
                    npcSkin.bones = teimoSkin.bones;
                    npcSkin.rootBone = teimoSkin.rootBone;

                    npcMesh.SetActive(true);
                    teimoMesh.SetActive(false);

                    GameObject npcObjectBase = npc.Body;

                    // Remove close shop FSM for non-Teimo characters.
                    GameObject hat = teimoRagdoll.transform.Find("pelvis/spine_mid/shoulders(xxxxx)/head/Accessories/teimo_hat").gameObject;
                    UnityEngine.Object.Destroy(hat);
                    PlayMakerFSM closeShopFsm = teimoRagdoll.GetComponent<PlayMakerFSM>();
                    UnityEngine.Object.Destroy(closeShopFsm);


                    // Set ragdoll body position.
                    // Set body/base.
                    teimoRagdoll.transform.position = npc.Skeleton.transform.position;
                    teimoRagdoll.transform.rotation = npc.Skeleton.transform.rotation;

                    // Spawn character specific attributes.
                    GameObject newHead = teimoRagdoll.transform.Find("pelvis/spine_mid/shoulders(xxxxx)/head").gameObject;
                    Vector3 propSize = new Vector3(0.1f, 0.1f, 0.1f);
                    void spawnNPCAttribute(GameObject attribute)
                    {
                        attribute.transform.parent = null;
                        attribute.transform.position = newHead.transform.position;
                        attribute.transform.rotation = newHead.transform.rotation;
                        attribute.AddComponent<Rigidbody>();
                        attribute.AddComponent<BoxCollider>().size = propSize;
                    }

                    if (npc.NpcObjectType == NpcType.Default)
                    {
                        GameObject glasses = newHead.transform.Find("Accessories/eye_glasses_regular").gameObject;
                        glasses.transform.parent = null;
                        glasses.AddComponent<Rigidbody>();
                        glasses.AddComponent<BoxCollider>().size = propSize;
                    }
                    else if (npc.NpcObjectType == NpcType.Ventti)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject venttiGlasses = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/eye_glasses_dark").gameObject;
                        spawnNPCAttribute(venttiGlasses);
                        GameObject venttiHat = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/latsa").gameObject;
                        spawnNPCAttribute(venttiHat);
                    }
                    else if (npc.NpcObjectType == NpcType.Grandma)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject grannyHat = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/hat_granny").gameObject;
                        spawnNPCAttribute(grannyHat);
                    }
                    else if (npc.NpcObjectType == NpcType.Drunk1)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject drunk1Cap = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/Cap 1").gameObject;
                        spawnNPCAttribute(drunk1Cap);
                    }
                    else if (npc.NpcObjectType == NpcType.Lindell || npc.NpcObjectType == NpcType.Guard || npc.NpcObjectType == NpcType.Synthist || npc.NpcObjectType == NpcType.SuskiPassenger || npc.NpcObjectType == NpcType.TheatreGuy || npc.NpcObjectType == NpcType.Jokke)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                    }
                    else if (npc.NpcObjectType == NpcType.Singer)
                    {
                        GameObject glasses = newHead.transform.Find("Accessories/eye_glasses_regular").gameObject;
                        glasses.transform.parent = null;
                        glasses.AddComponent<Rigidbody>();
                        glasses.AddComponent<BoxCollider>().size = propSize;
                        GameObject cowboyHat = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/cowboy_hat").gameObject;
                        spawnNPCAttribute(cowboyHat);
                    }
                    else if (npc.NpcObjectType == NpcType.Strawberryman)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject strawberryHat = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/Accessories 1/Hats/Latsa").gameObject;
                        spawnNPCAttribute(strawberryHat);
                    }
                    else if (npc.NpcObjectType == NpcType.ShitNPC)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject beerBottle = npc.Skeleton.transform.Find("pelvis/RotationPivot/spine_middle/spine_upper/collar_right/shoulder_right/arm_right/hand_right/BottleBeerFly").gameObject;
                        spawnNPCAttribute(beerBottle);
                    }
                    else if (npc.NpcObjectType == NpcType.Jani)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject janiHat = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/Cap").gameObject;
                        spawnNPCAttribute(janiHat);
                    }
                    else if (npc.NpcObjectType == NpcType.Petteri)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject petteriHat = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/Cap").gameObject;
                        spawnNPCAttribute(petteriHat);
                    }
                    else if (npc.NpcObjectType == NpcType.BusDriver)
                    {
                        GameObject glasses = newHead.transform.Find("Accessories/eye_glasses_regular").gameObject;
                        glasses.transform.parent = null;
                        glasses.AddComponent<Rigidbody>();
                        glasses.AddComponent<BoxCollider>().size = propSize;
                        GameObject busDriverHat = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/accessories/hat_busdriver").gameObject;
                        spawnNPCAttribute(busDriverHat);
                    }
                    else if (npc.NpcObjectType == NpcType.SigneTheatre)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject glasses = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/eye_glasses_dark").gameObject;
                        spawnNPCAttribute(glasses);
                    }
                    else if (npc.NpcObjectType == NpcType.MarkkuTheatre)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject markkuHat = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/Cap 2/hat_fish").gameObject;
                        spawnNPCAttribute(markkuHat);
                    }
                    else if (npc.NpcObjectType == NpcType.SoccerKid)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject kidHat = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/Cap 1").gameObject;
                        spawnNPCAttribute(kidHat);
                    }
                    else if (npc.NpcObjectType == NpcType.PartsSalesman)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject shades = npc.Skeleton.transform.Find("pelvis/RotationPivot/spine_middle/spine_upper/head/Shades 1").gameObject;
                        spawnNPCAttribute(shades);
                        GameObject cap = npc.Skeleton.transform.Find("pelvis/RotationPivot/spine_middle/spine_upper/head/Cap1").gameObject;
                        spawnNPCAttribute(cap);
                    }
                    else if (npc.NpcObjectType == NpcType.TheatreCop)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject copCap = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/Cap 1").gameObject;
                        spawnNPCAttribute(copCap);
                        GameObject copShades = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/Shades 1").gameObject;
                        spawnNPCAttribute(copShades);
                    }
                    else if (npc.NpcObjectType == NpcType.theatrePresident)
                    {
                        UnityEngine.Object.Destroy(newHead.transform.Find("Accessories/eye_glasses_regular").gameObject);
                        GameObject presidentHat = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/latsa 1").gameObject;
                        spawnNPCAttribute(presidentHat);
                        GameObject presidentGlasses = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head/eye_glasses_dark 1").gameObject;
                        spawnNPCAttribute(presidentGlasses);
                    }
                    else if (npc.NpcObjectType == NpcType.Uncle)
                    {
                        GameObject uncleHat = npc.Skeleton.transform.Find("pelvis/spine_middle/spine_upper/HeadPivot/head").gameObject;
                        spawnNPCAttribute(uncleHat);
                    }


                    // Activate ragdoll.
                    npcObjectBase.SetActive(false);
                    UnityEngine.Object.Destroy(npcObjectBase);
                    teimoRagdoll.SetActive(true);

                    AudioSource.PlayClipAtPoint(punchSound, camera.transform.position);

                    if (this.becomeWanted.GetValue())
                    {
                        PlayMakerFSM crimePm = crime.GetPlayMaker("Activate");
                        crimePm.Fsm.GetFsmInt("AttemptedManslaughter").Value += 1;
                    }

                    npc.punched();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private void initTeimo()
        {
            this.GetGameObject("STORE/TeimoInShop/Pivot");
            this.GetGameObject("STORE/TeimoInShop/Pivot/FacePissTrigger");
            this.GetGameObject("STORE/TeimoInShop/Pivot/Teimo/skeleton");
        }

        private void initFleetari()
        {
            GameObject fleetariMesh = this.GetGameObject("REPAIRSHOP/LOD/Office/Fleetari/Neighbour 2/bodymesh");
            GameObject fleetariHead = this.GetGameObject("REPAIRSHOP/LOD/Office/Fleetari/Neighbour 2/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            GameObject fleetariBody = this.GetGameObject("REPAIRSHOP/LOD/Office/Fleetari/Neighbour 2");
            GameObject fleetariSkeleton = this.GetGameObject("REPAIRSHOP/LOD/Office/Fleetari/Neighbour 2/skeleton");
            void done()
            {

            }

            this.initNPC(fleetariHead, fleetariMesh, fleetariBody, fleetariSkeleton, done);
        }

        private void initFleetariRallySaturday()
        {
            GameObject fleetariMesh = this.GetGameObject("RALLY/Saturday/StartArea/StartAreaObjects/FleetariRally/Neighbour/bodymesh");
            GameObject fleetariHead = this.GetGameObject("RALLY/Saturday/StartArea/StartAreaObjects/FleetariRally/Neighbour/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            GameObject fleetariBody = this.GetGameObject("RALLY/Saturday/StartArea/StartAreaObjects/FleetariRally/Neighbour");
            GameObject fleetariSkeleton = this.GetGameObject("RALLY/Saturday/StartArea/StartAreaObjects/FleetariRally/Neighbour/skeleton");
            void done()
            {

            }

            this.initNPC(fleetariHead, fleetariMesh, fleetariBody, fleetariSkeleton, done);
        }

        private void initFleetariRallySunday()
        {
            GameObject fleetariMesh = this.GetGameObject("RALLY/Sunday/StartArea/StartAreaObjects/FleetariRally/Neighbour/bodymesh");
            GameObject fleetariHead = this.GetGameObject("RALLY/Sunday/StartArea/StartAreaObjects/FleetariRally/Neighbour/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            GameObject fleetariBody = this.GetGameObject("RALLY/Sunday/StartArea/StartAreaObjects/FleetariRally/Neighbour");
            GameObject fleetariSkeleton = this.GetGameObject("RALLY/Sunday/StartArea/StartAreaObjects/FleetariRally/Neighbour/skeleton");
            void done()
            {

            }

            this.initNPC(fleetariHead, fleetariMesh, fleetariBody, fleetariSkeleton, done);
        }

        private void initShitman()
        {
            GameObject shitmanMesh = this.GetGameObject("WATERFACILITY/LOD/Functions/Servant/Pivot/Shitman/bodymesh");
            GameObject shitmanHeadAkaShithead = this.GetGameObject("WATERFACILITY/LOD/Functions/Servant/Pivot/Shitman/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            GameObject shitmanBody = this.GetGameObject("WATERFACILITY/LOD/Functions/Servant/Pivot/Shitman");
            GameObject shitmanSkeleton = this.GetGameObject("WATERFACILITY/LOD/Functions/Servant/Pivot/Shitman/skeleton");
            void done()
            {

            }
            this.initNPC(shitmanHeadAkaShithead, shitmanMesh, shitmanBody, shitmanSkeleton, done);
        }

        private void initVenttipig()
        {
            GameObject venttiPig = this.GetGameObject("CABIN/Cabin/Ventti/PIG/VenttiPig/Pivot/Char");
            GameObject venttiPigHead = this.GetGameObject("CABIN/Cabin/Ventti/PIG/VenttiPig/Pivot/Char/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            GameObject venttiPigBodyMesh = this.GetGameObject("CABIN/Cabin/Ventti/PIG/VenttiPig/Pivot/Char/bodymesh");
            GameObject venttiPigSkeleton = this.GetGameObject("CABIN/Cabin/Ventti/PIG/VenttiPig/Pivot/Char/skeleton");
            //GameObject venttiPigHead = venttiPigSkeleton;// Use the skeleton because the head hitbox is weird.
            void done()
            {

            }
            this.initNPC(venttiPigHead, venttiPigBodyMesh, venttiPig, venttiPigSkeleton, done, NpcType.Ventti);
        }

        private void initGrandma()
        {
            GameObject grandmaBase = this.GetGameObject("JOBS/Mummola/LOD/GrannyTalking/Granny");
            GameObject grandmaHead = this.GetGameObject("JOBS/Mummola/LOD/GrannyTalking/Granny/Char/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            GameObject grandmaSkeleton = this.GetGameObject("JOBS/Mummola/LOD/GrannyTalking/Granny/Char/skeleton");
            GameObject grandmaBodymesh = this.GetGameObject("JOBS/Mummola/LOD/GrannyTalking/Granny/Char/bodymesh");
            void done()
            {

            }
            this.initNPC(grandmaHead, grandmaBodymesh, grandmaBase, grandmaSkeleton, done, NpcType.Grandma);
        }

        private void initDrunk1()
        {
            GameObject drunk1Base = this.GetGameObject("STORE/LOD/ActivateBar/DrunksBar/Drunk1");
            GameObject drunk1Head = this.GetGameObject("STORE/LOD/ActivateBar/DrunksBar/Drunk1/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            //GameObject drunk1Head = this.GetGameObject("STORE/LOD/ActivateBar/DrunksBar/Drunk1/skeleton/pelvis/spine_middle/spine_upper");
            GameObject drunk1Skeleton = this.GetGameObject("STORE/LOD/ActivateBar/DrunksBar/Drunk1/skeleton");
            GameObject drunk1Bodymesh = this.GetGameObject("STORE/LOD/ActivateBar/DrunksBar/Drunk1/bodymesh");
            void done()
            {

            }
            this.initNPC(drunk1Head, drunk1Bodymesh, drunk1Base, drunk1Skeleton, done, NpcType.Drunk1);
        }

        private void initDrunk2()
        {
            GameObject drunk2Base = this.GetGameObject("STORE/LOD/ActivateBar/DrunksBar/Drunk2");
            GameObject drunk2Head = this.GetGameObject("STORE/LOD/ActivateBar/DrunksBar/Drunk2/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            //GameObject drunk2Head = this.GetGameObject("STORE/LOD/ActivateBar/DrunksBar/Drunk2/skeleton/pelvis/spine_middle/spine_upper");
            GameObject drunk2Skeleton = this.GetGameObject("STORE/LOD/ActivateBar/DrunksBar/Drunk2/skeleton");
            GameObject drunk2Bodymesh = this.GetGameObject("STORE/LOD/ActivateBar/DrunksBar/Drunk2/bodymesh");
            void done()
            {

            }
            this.initNPC(drunk2Head, drunk2Bodymesh, drunk2Base, drunk2Skeleton, done);
        }

        private void initLindell()
        {
            GameObject lindellBase = this.GetGameObject("INSPECTION/LOD/Officer/Work/Char");
            GameObject lindellBodymesh = this.GetGameObject("INSPECTION/LOD/Officer/Work/Char/bodymesh");
            GameObject lindellHead = this.GetGameObject("INSPECTION/LOD/Officer/Work/Char/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            GameObject lindellSkeleton = this.GetGameObject("INSPECTION/LOD/Officer/Work/Char/skeleton");
            void done()
            {

            }
            this.initNPC(lindellHead, lindellBodymesh, lindellBase, lindellSkeleton, done, NpcType.Lindell);
        }

        private void initGuard()
        {
            GameObject guardBase = this.GetGameObject("DANCEHALL/Functions/GUARD/Guard");
            GameObject guardBodymesh = this.GetGameObject("DANCEHALL/Functions/GUARD/Guard/Pivot/Char/bodymesh");
            GameObject guardHead = this.GetGameObject("DANCEHALL/Functions/GUARD/Guard/Pivot/Char/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            GameObject guardSkeleton = this.GetGameObject("DANCEHALL/Functions/GUARD/Guard/Pivot/Char/skeleton");
            void done()
            {

            }
            this.initNPC(guardHead, guardBodymesh, guardBase, guardSkeleton, done, NpcType.Guard);
        }

        private void initSinger()
        {
            GameObject singerBase = this.GetGameObject("DANCEHALL/Functions/BAND/Singer");
            GameObject singerBodymesh = this.GetGameObject("DANCEHALL/Functions/BAND/Singer/Pivot/Char/bodymesh");
            GameObject singerSkeleton = this.GetGameObject("DANCEHALL/Functions/BAND/Singer/Pivot/Char/skeleton");
            GameObject singerHead = this.GetGameObject("DANCEHALL/Functions/BAND/Singer/Pivot/Char/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            //GameObject singerHead = this.GetGameObject("DANCEHALL/Functions/BAND/Singer/Pivot");
            void done()
            {

            }
            this.initNPC(singerHead, singerBodymesh, singerBase, singerSkeleton, done, NpcType.Singer);
        }

        private void initSynthist()
        {
            GameObject synthistBase = this.GetGameObject("DANCEHALL/Functions/BAND/SYNTH/Synthist");
            GameObject synthistBodymesh = this.GetGameObject("DANCEHALL/Functions/BAND/SYNTH/Synthist/bodymesh");
            GameObject synthistSkeleton = this.GetGameObject("DANCEHALL/Functions/BAND/SYNTH/Synthist/skeleton");
            GameObject synthistHead = this.GetGameObject("DANCEHALL/Functions/BAND/SYNTH/Synthist/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(synthistHead, synthistBodymesh, synthistBase, synthistSkeleton, done, NpcType.Synthist);
        }

        private void initBassist()
        {
            GameObject bassistBase = this.GetGameObject("DANCEHALL/Functions/BAND/Bassist");
            GameObject bassistBodymesh = this.GetGameObject("DANCEHALL/Functions/BAND/Bassist/Pivot/Char/bodymesh");
            GameObject bassistSkeleton = this.GetGameObject("DANCEHALL/Functions/BAND/Bassist/Pivot/Char/skeleton");
            GameObject bassistHead = this.GetGameObject("DANCEHALL/Functions/BAND/Bassist/Pivot/Char/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(bassistHead, bassistBodymesh, bassistBase, bassistSkeleton, done);
        }

        private void initDrummer()
        {
            GameObject drummerBase = this.GetGameObject("DANCEHALL/Functions/BAND/DRUMS/Drummer");
            GameObject drummerBodymesh = this.GetGameObject("DANCEHALL/Functions/BAND/DRUMS/Drummer/bodymesh");
            GameObject drummerSkeleton = this.GetGameObject("DANCEHALL/Functions/BAND/DRUMS/Drummer/skeleton");
            GameObject drummerHead = this.GetGameObject("DANCEHALL/Functions/BAND/DRUMS/Drummer/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(drummerHead, drummerBodymesh, drummerBase, drummerSkeleton, done);
        }

        private void initJani()
        {
            GameObject janiBase = this.GetGameObject("NPC_CARS/Amikset/KYLAJANI/Driver/Driver");
            GameObject janiSkeleton = this.GetGameObject("NPC_CARS/Amikset/KYLAJANI/Driver/Driver/skeleton");
            GameObject janiBodymesh = this.GetGameObject("NPC_CARS/Amikset/KYLAJANI/Driver/Driver/bodymesh");
            GameObject janiHead = this.GetGameObject("NPC_CARS/Amikset/KYLAJANI/Driver/Driver/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");

            GameObject janiNavAi = this.GetGameObject("NPC_CARS/Amikset/KYLAJANI/NavigationAI");
            GameObject janiColAi = this.GetGameObject("NPC_CARS/Amikset/KYLAJANI/CarColliderAI");
            void done()
            {
                // Disable the driving AI.
                janiNavAi.SetActive(false);
                janiColAi.SetActive(false);
            }
            this.initNPC(janiHead, janiBodymesh, janiBase, janiSkeleton, done, NpcType.Jani);
        }

        private void initPetteri()
        {
            GameObject petteriBase = this.GetGameObject("NPC_CARS/Amikset/AMIS2/Passengers 3");
            GameObject petteriSkeleton = this.GetGameObject("NPC_CARS/Amikset/AMIS2/Passengers 3/Driver/skeleton");
            GameObject petteriBodymesh = this.GetGameObject("NPC_CARS/Amikset/AMIS2/Passengers 3/Driver/bodymesh");
            GameObject petteriHead = this.GetGameObject("NPC_CARS/Amikset/AMIS2/Passengers 3/Driver/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");

            GameObject petteriNavAi = this.GetGameObject("NPC_CARS/Amikset/AMIS2/NavigationAI");
            GameObject petteriColAi = this.GetGameObject("NPC_CARS/Amikset/AMIS2/CarColliderAI");
            void done()
            {
                // Disable the driving AI.
                petteriNavAi.SetActive(false);
                petteriColAi.SetActive(false);
            }
            this.initNPC(petteriHead, petteriBodymesh, petteriBase, petteriSkeleton, done, NpcType.Petteri);
        }

        private void initSuskiPassenger()
        {
            GameObject suskiBase = this.GetGameObject("NPC_CARS/Amikset/KYLAJANI/LOD/Passenger");
            GameObject suskiSkeleton = this.GetGameObject("NPC_CARS/Amikset/KYLAJANI/LOD/Passenger/skeleton");
            GameObject suskiBodymesh = this.GetGameObject("NPC_CARS/Amikset/KYLAJANI/LOD/Passenger/bodymesh");
            GameObject suskiHead = this.GetGameObject("NPC_CARS/Amikset/KYLAJANI/LOD/Passenger/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(suskiHead, suskiBodymesh, suskiBase, suskiSkeleton, done, NpcType.SuskiPassenger);
        }

        private void initStrawberryman()
        {
            GameObject strawBase = this.GetGameObject("JOBS/StrawberryField/LOD/Functions/Berryman");
            GameObject strawHead = this.GetGameObject("JOBS/StrawberryField/LOD/Functions/Berryman/Pivot/Berryman/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            GameObject strawBodymesh = this.GetGameObject("JOBS/StrawberryField/LOD/Functions/Berryman/Pivot/Berryman/bodymesh");
            GameObject strawSkeleton = this.GetGameObject("JOBS/StrawberryField/LOD/Functions/Berryman/Pivot/Berryman/skeleton");
            void done()
            {

            }
            this.initNPC(strawHead, strawBodymesh, strawBase, strawSkeleton, done, NpcType.Strawberryman);
        }

        private void initBusDriver()
        {
            GameObject busBase = this.GetGameObject("BUS/LOD/Driver");
            GameObject busHead = this.GetGameObject("BUS/LOD/Driver/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            GameObject busSkeleton = this.GetGameObject("BUS/LOD/Driver/skeleton");
            GameObject busBodymesh = this.GetGameObject("BUS/LOD/Driver/bodymesh");

            GameObject busDriverNavAi = this.GetGameObject("BUS/NavigationAI");
            void done()
            {
                // Disable the driving AI.
                busDriverNavAi.SetActive(false);
            }
            this.initNPC(busHead, busBodymesh, busBase, busSkeleton, done, NpcType.BusDriver);
        }

        private void initShitNPC1()
        {
            GameObject npcBase = this.GetGameObject("JOBS/HouseShit1/LOD/ShitNPC/ShitMan1");
            GameObject npcHead = this.GetGameObject("JOBS/HouseShit1/LOD/ShitNPC/ShitMan1/skeleton/pelvis/RotationPivot/spine_middle/spine_upper/head");
            GameObject npcBodymesh = this.GetGameObject("JOBS/HouseShit1/LOD/ShitNPC/ShitMan1/bodymesh");
            GameObject npcSkeleton = this.GetGameObject("JOBS/HouseShit1/LOD/ShitNPC/ShitMan1/skeleton");
            void done()
            {

            }
            this.initNPC(npcHead, npcBodymesh, npcBase, npcSkeleton, done, NpcType.ShitNPC);
        }
        private void initShitNPC2()
        {
            GameObject npcBase = this.GetGameObject("JOBS/HouseShit2/LOD/ShitNPC/ShitMan2");
            GameObject npcHead = this.GetGameObject("JOBS/HouseShit2/LOD/ShitNPC/ShitMan2/skeleton/pelvis/RotationPivot/spine_middle/spine_upper/head");
            GameObject npcBodymesh = this.GetGameObject("JOBS/HouseShit2/LOD/ShitNPC/ShitMan2/bodymesh");
            GameObject npcSkeleton = this.GetGameObject("JOBS/HouseShit2/LOD/ShitNPC/ShitMan2/skeleton");
            void done()
            {

            }
            this.initNPC(npcHead, npcBodymesh, npcBase, npcSkeleton, done, NpcType.ShitNPC);
        }
        private void initShitNPC3()
        {
            GameObject npcBase = this.GetGameObject("JOBS/HouseShit3/LOD/ShitNPC/ShitMan3");
            GameObject npcHead = this.GetGameObject("JOBS/HouseShit3/LOD/ShitNPC/ShitMan3/skeleton/pelvis/RotationPivot/spine_middle/spine_upper/head");
            GameObject npcBodymesh = this.GetGameObject("JOBS/HouseShit3/LOD/ShitNPC/ShitMan3/bodymesh");
            GameObject npcSkeleton = this.GetGameObject("JOBS/HouseShit3/LOD/ShitNPC/ShitMan3/skeleton");
            void done()
            {

            }
            this.initNPC(npcHead, npcBodymesh, npcBase, npcSkeleton, done, NpcType.ShitNPC);
        }
        private void initShitNPC4()
        {
            GameObject npcBase = this.GetGameObject("JOBS/HouseShit4/LOD/ShitNPC/ShitMan4");
            GameObject npcHead = this.GetGameObject("JOBS/HouseShit4/LOD/ShitNPC/ShitMan4/skeleton/pelvis/RotationPivot/spine_middle/spine_upper/head");
            GameObject npcBodymesh = this.GetGameObject("JOBS/HouseShit4/LOD/ShitNPC/ShitMan4/bodymesh");
            GameObject npcSkeleton = this.GetGameObject("JOBS/HouseShit4/LOD/ShitNPC/ShitMan4/skeleton");
            void done()
            {

            }
            this.initNPC(npcHead, npcBodymesh, npcBase, npcSkeleton, done, NpcType.ShitNPC);
        }
        private void initShitNPC5()
        {
            GameObject npcBase = this.GetGameObject("JOBS/HouseShit5/LOD/ShitNPC/ShitMan5");
            GameObject npcHead = this.GetGameObject("JOBS/HouseShit5/LOD/ShitNPC/ShitMan5/skeleton/pelvis/RotationPivot/spine_middle/spine_upper/head");
            GameObject npcBodymesh = this.GetGameObject("JOBS/HouseShit5/LOD/ShitNPC/ShitMan5/bodymesh");
            GameObject npcSkeleton = this.GetGameObject("JOBS/HouseShit5/LOD/ShitNPC/ShitMan5/skeleton");
            void done()
            {

            }
            this.initNPC(npcHead, npcBodymesh, npcBase, npcSkeleton, done, NpcType.ShitNPC);
        }

        private void initSigne()
        {
            GameObject signeBase = this.GetGameObject("THEATRE/LOD/Open/Audience/SigneTheatre");
            GameObject signeSkeleton = this.GetGameObject("THEATRE/LOD/Open/Audience/SigneTheatre/Char/skeleton");
            GameObject signeBodymesh = this.GetGameObject("THEATRE/LOD/Open/Audience/SigneTheatre/Char/bodymesh");
            GameObject signeHead = this.GetGameObject("THEATRE/LOD/Open/Audience/SigneTheatre/Char/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(signeHead, signeBodymesh, signeBase, signeSkeleton, done, NpcType.SigneTheatre);
        }

        private void initMarkku()
        {
            GameObject markkuBase = this.GetGameObject("THEATRE/LOD/Open/Audience/MarkkuTheatre");
            GameObject markkuSkeleton = this.GetGameObject("THEATRE/LOD/Open/Audience/MarkkuTheatre/Pivot/skeleton");
            GameObject markkuBodymesh = this.GetGameObject("THEATRE/LOD/Open/Audience/MarkkuTheatre/Pivot/bodymesh");
            GameObject markkuHead = this.GetGameObject("THEATRE/LOD/Open/Audience/MarkkuTheatre/Pivot/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(markkuHead, markkuBodymesh, markkuBase, markkuSkeleton, done, NpcType.MarkkuTheatre);
        }

        private void initSoccerKid1()
        {
            GameObject kidBase = this.GetGameObject("SOCCER/LOD/KID1");
            GameObject kidSkeleton = this.GetGameObject("SOCCER/LOD/KID1/Offset/Pivot/skeleton");
            GameObject kidBodymesh = this.GetGameObject("SOCCER/LOD/KID1/Offset/Pivot/bodymesh_kid");
            GameObject kidHead = this.GetGameObject("SOCCER/LOD/KID1/Offset/Pivot/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(kidHead, kidBodymesh, kidBase, kidSkeleton, done, NpcType.SoccerKid);
        }

        private void initSoccerKid2()
        {
            GameObject kidBase = this.GetGameObject("SOCCER/LOD/KID2");
            GameObject kidSkeleton = this.GetGameObject("SOCCER/LOD/KID2/Offset/Pivot/skeleton");
            GameObject kidBodymesh = this.GetGameObject("SOCCER/LOD/KID2/Offset/Pivot/bodymesh_kid");
            GameObject kidHead = this.GetGameObject("SOCCER/LOD/KID2/Offset/Pivot/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(kidHead, kidBodymesh, kidBase, kidSkeleton, done, NpcType.SoccerKid);
        }

        private void initPartsSalesman()
        {
            GameObject manBase = this.GetGameObject("RALLY/PartsSalesman/Functions/Man");
            GameObject manSkeleton = this.GetGameObject("RALLY/PartsSalesman/Functions/Man/skeleton");
            GameObject manHead = this.GetGameObject("RALLY/PartsSalesman/Functions/Man/skeleton/pelvis/RotationPivot/spine_middle/spine_upper/head");
            GameObject manBodymesh = this.GetGameObject("RALLY/PartsSalesman/Functions/Man/bodymesh");
            void done()
            {

            }
            this.initNPC(manBase, manBodymesh, manBase, manSkeleton, done, NpcType.PartsSalesman);
        }

        private void initPena()
        {
            GameObject penaBase = this.GetGameObject("TRAFFIC/VehiclesDirtRoad/Rally/FITTAN/Driver");
            GameObject penaSkeleton = this.GetGameObject("TRAFFIC/VehiclesDirtRoad/Rally/FITTAN/Driver/skeleton");
            GameObject penaBodymesh = this.GetGameObject("TRAFFIC/VehiclesDirtRoad/Rally/FITTAN/Driver/bodymesh");
            GameObject penaHead = this.GetGameObject("TRAFFIC/VehiclesDirtRoad/Rally/FITTAN/Driver/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");

            GameObject penaNavAi = this.GetGameObject("TRAFFIC/VehiclesDirtRoad/Rally/FITTAN/Navigation");
            GameObject penaColAi = this.GetGameObject("TRAFFIC/VehiclesDirtRoad/Rally/FITTAN/CarColliderAI");
            void done()
            {
                penaNavAi.SetActive(false);
                penaColAi.SetActive(false);
            }
            this.initNPC(penaHead, penaBodymesh, penaBase, penaSkeleton, done);
        }

        private void initPenaJail()
        {
            GameObject penaBase = this.GetGameObject("JAIL/Cousin");
            GameObject penaSkeleton = this.GetGameObject("JAIL/Cousin/skeleton");
            GameObject penaBodymesh = this.GetGameObject("JAIL/Cousin/bodymesh");
            GameObject penaHead = this.GetGameObject("JAIL/Cousin/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(penaHead, penaBodymesh, penaBase, penaSkeleton, done);
        }

        private void initUncle()
        {
            GameObject uncleBase = this.GetGameObject("YARD/UNCLE/Home/UncleDrinking/Uncle/Pivot/Char");
            GameObject uncleSkeleton = this.GetGameObject("YARD/UNCLE/Home/UncleDrinking/Uncle/Pivot/Char/skeleton");
            GameObject uncleBodymesh = this.GetGameObject("YARD/UNCLE/Home/UncleDrinking/Uncle/Pivot/Char/bodymesh");
            GameObject uncleHead = this.GetGameObject("YARD/UNCLE/Home/UncleDrinking/Uncle/Pivot/Char/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(uncleHead, uncleBodymesh, uncleBase, uncleSkeleton, done, NpcType.Uncle);
        }

        private void initTheatreCop()
        {
            GameObject copBase = this.GetGameObject("COP");
            GameObject copSkeleton = this.GetGameObject("COP/PIVOT/skeleton");
            GameObject copBodymesh = this.GetGameObject("COP/PIVOT/bodymesh");
            GameObject copHead = this.GetGameObject("COP/PIVOT/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(copHead, copBodymesh, copBase, copSkeleton, done, NpcType.TheatreCop);
        }

        private void initTheatreGuy()
        {
            GameObject guyBase = this.GetGameObject("MARA");
            GameObject guySkeleton = this.GetGameObject("MARA/PIVOT/skeleton");
            GameObject guyMesh = this.GetGameObject("MARA/PIVOT/bodymesh");
            GameObject guyHead = this.GetGameObject("MARA/PIVOT/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(guyHead, guyMesh, guyBase, guySkeleton, done, NpcType.TheatreGuy);
        }

        private void initTheatrePresident()
        {
            GameObject copBase = this.GetGameObject("PRESIDENT");
            GameObject copSkeleton = this.GetGameObject("PRESIDENT/PIVOT/skeleton");
            GameObject copBodymesh = this.GetGameObject("PRESIDENT/PIVOT/bodymesh");
            GameObject copHead = this.GetGameObject("PRESIDENT/PIVOT/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(copHead, copBodymesh, copBase, copSkeleton, done, NpcType.theatrePresident);
        }

        private void initJokke()
        {
            GameObject jokkeBase = this.GetGameObject("BeerCamp/KiljuBuyer/Char");
            GameObject jokkeSkeleton = this.GetGameObject("BeerCamp/KiljuBuyer/Char/skeleton");
            GameObject jokkeBodymesh = this.GetGameObject("BeerCamp/KiljuBuyer/Char/bodymesh");
            GameObject jokkeHead = this.GetGameObject("BeerCamp/KiljuBuyer/Char/skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head");
            void done()
            {

            }
            this.initNPC(jokkeHead, jokkeBodymesh, jokkeBase, jokkeSkeleton, done, NpcType.Jokke);
        }

        private bool punchTeimo()
        {
            if (!this.teimoPunched)
            {
                // Teimo has his own function because he works slightly different.

                GameObject teimoInStore = this.GetGameObject("STORE/TeimoInShop/Pivot");
                if (teimoInStore == null)
                {
                    return false;
                }
                GameObject facePissTrigger = this.GetGameObject("STORE/TeimoInShop/Pivot/FacePissTrigger");
                if (facePissTrigger == null)
                {
                    return false;
                }

                RaycastHit hit;
                if (Physics.Raycast(camera.transform.position, camera.transform.forward, out hit, 1.0f))
                {
                    if (hit.transform == facePissTrigger.transform)
                    {
                        // Set Teimo ragdoll position.
                        this.teimoPunched = true;
                        GameObject teimoInBikeRagdollDupe = GameObject.Instantiate(teimoInBikeRagdollBase);
                        teimoInBikeRagdollDupe.transform.parent = null;// teimoInStore.transform;

                        // Set ragdoll body position.
                        GameObject teimoSkeleton = this.GetGameObject("STORE/TeimoInShop/Pivot/Teimo/skeleton");
                        // Set body/base.
                        teimoInBikeRagdollDupe.transform.position = teimoSkeleton.transform.position;
                        teimoInBikeRagdollDupe.transform.rotation = teimoSkeleton.transform.rotation;

                        Vector3 propSize = new Vector3(0.1f, 0.1f, 0.1f);
                        GameObject newHead = teimoInBikeRagdollDupe.transform.Find("pelvis/spine_mid/shoulders(xxxxx)/head").gameObject;
                        GameObject hat = teimoInBikeRagdollDupe.transform.Find("pelvis/spine_mid/shoulders(xxxxx)/head/Accessories/teimo_hat").gameObject;
                        hat.transform.parent = null;
                        hat.AddComponent<Rigidbody>();
                        hat.AddComponent<BoxCollider>().size = propSize;
                        GameObject glasses = newHead.transform.Find("Accessories/eye_glasses_regular").gameObject;
                        glasses.transform.parent = null;
                        glasses.AddComponent<Rigidbody>();
                        glasses.AddComponent<BoxCollider>().size = propSize;

                        teimoInBikeRagdollDupe.SetActive(true);
                        AudioSource.PlayClipAtPoint(punchSound, camera.transform.position);

                        if (this.becomeWanted.GetValue())
                        {
                            PlayMakerFSM crimePm = crime.GetPlayMaker("Activate");
                            crimePm.Fsm.GetFsmInt("AttemptedManslaughter").Value += 1;
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private void initAll()
        {
            this.NPCs = new List<PunchNPC>();
            this.initTeimo();
            this.initFleetari();
            this.initFleetariRallySaturday();
            this.initFleetariRallySunday();
            this.initShitman();
            this.initVenttipig();
            this.initGrandma();
            this.initDrunk1();
            this.initDrunk2();
            this.initLindell();
            this.initGuard();
            this.initSinger();
            this.initSynthist();
            this.initBassist();
            this.initDrummer();
            this.initJani();
            this.initPetteri();
            this.initSuskiPassenger();
            this.initBusDriver();
            this.initStrawberryman();
            this.initShitNPC1();
            this.initShitNPC2();
            this.initShitNPC3();
            this.initShitNPC4();
            this.initShitNPC5();
            this.initSigne();
            this.initMarkku();
            this.initSoccerKid1();
            this.initSoccerKid2();
            this.initPartsSalesman();
            this.initPena();
            this.initPenaJail();
            this.initUncle();
            this.initTheatreCop();
            this.initTheatreGuy();
            this.initTheatrePresident();
            this.initJokke();
        }

        private void punchHandler()
        {
            if (this.punchTeimo())
            {
                return;
            }

            RaycastHit hit;
            if (Physics.Raycast(camera.transform.position, camera.transform.forward, out hit, 1.0f))
            {
                foreach (PunchNPC npc in this.NPCs)
                {
                    if (this.punchNPC(npc, hit))
                    {
                        break;
                    }
                }
            }
        }
    }
}
