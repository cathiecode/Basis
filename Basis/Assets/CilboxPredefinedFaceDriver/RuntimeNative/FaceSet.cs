using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.superneko.basis.masque.cilbox
{
    [Serializable]
    public class Expression
    {
        [HideInInspector] public int ReferenceId;
        public AnimationClip AnimationClip;
    }

    [Serializable]
    public class FaceSet
    {
        public AnimationClip DefaultExpressionAnimationClip;
        public List<Expression> Expressions = new();
    }
}
