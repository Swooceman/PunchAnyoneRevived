using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PunchAnyoneRevived
{
    internal class PunchNPC
    {
        public GameObject Head;
        public GameObject Mesh;
        public GameObject Body;
        public GameObject Skeleton;
        public Action punched;
        public NpcType NpcObjectType;

        public PunchNPC() {
            
        }
    }
}
