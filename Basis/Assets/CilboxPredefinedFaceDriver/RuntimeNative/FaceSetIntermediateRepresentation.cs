using System;
using System.Collections.Generic;

namespace com.superneko.basis.masque.native
{
    [Serializable]
    public struct SerializableExpression
    {
        public int ReferenceId;
        public float[] BlendshapeWeights; // Weight of blendShape. Layout is same as controllingBlendShapes
        public string[] Tags;

        HashSet<string> _tagsSet;
        internal HashSet<string> TagsSet
        {
            get
            {
                _tagsSet ??= new HashSet<string>(Tags);

                return _tagsSet;
            }
        }
    }

    [Serializable]
    public struct SerializableFaceSet
    {
        public int[] ControllingBlendShapes; // List of blendShape index
        public SerializableExpression DefaultExpression;
        public SerializableExpression[] Expressions;
    }
}
