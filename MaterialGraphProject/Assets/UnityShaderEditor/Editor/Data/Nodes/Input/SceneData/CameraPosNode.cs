using UnityEngine;
using UnityEditor.Graphing;

namespace UnityEditor.ShaderGraph
{
    [Title("Input/Scene Data/Camera Position")]
    public class CamPosNode : AbstractMaterialNode
    {
        //TODO - should be a global and immpiment a Imayrequire
        public CamPosNode()
        {
            name = "CameraPosition";
            UpdateNodeAfterDeserialization();
        }

        private const int kOutputSlotId = 0;
        private const string kOutputSlotName = "Output";

        public override bool hasPreview { get { return true; } }
        public override PreviewMode previewMode
        {
            get { return PreviewMode.Preview2D; }
        }


        public sealed override void UpdateNodeAfterDeserialization()
        {
            AddSlot(new Vector4MaterialSlot(kOutputSlotId, kOutputSlotName, kOutputSlotName, SlotType.Output, Vector4.zero));
            RemoveSlotsNameNotMatching(new[] { kOutputSlotId });
        }

        public override string GetVariableNameForSlot(int slotId)
        {
            return "_WorldSpaceCameraPos";
        }
    }
}
