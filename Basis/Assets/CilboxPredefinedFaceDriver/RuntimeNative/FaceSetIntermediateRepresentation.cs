using System;
using System.Collections.Generic;

namespace com.superneko.basis.masque.native
{
    [Serializable]
    public struct SerializableExpression
    {
        public int ReferenceId;
        public float[] blendshapeWeights; // Weight of blendShape. Layout is same as controllingBlendShapes
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
        public int[] controllingBlendShapes; // List of blendShape index
        public SerializableExpression defaultExpression;
        public SerializableExpression[] expressions;
    }
}
