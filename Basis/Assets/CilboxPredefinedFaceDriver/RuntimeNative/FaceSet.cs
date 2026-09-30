using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.superneko.basis.masque.cilbox
{
    [Serializable]
    public class Expression
    {
        public AnimationClip AnimationClip;

        public string[] Tags;
    }

    [Serializable]
    public class FaceSet
    {
        public AnimationClip DefaultExpressionAnimationClip;
        public List<Expression> Expressions = new();
    }
}
