/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 What a button plays: music, sound effects or both (the small badges of the buttons).
*/
using Ares.Data;

namespace Ares.AppCore
{
    public static class ElementKinds
    {
        public const int Music = 1;
        public const int Effect = 2;

        /// <summary>Flags: 1 = music, 2 = sound effects (0 = nothing known).</summary>
        public static int Of(IElement element)
        {
            return Of(element, 0);
        }

        public static int Of(IElement element, int depth)
        {
            if (element == null || depth > 12)
                return 0;
            if (element is IFileElement)
                return ((IFileElement)element).SoundFileType == SoundFileType.Music ? Music : Effect;
            if (element is IWebRadioElement || element is IMusicList)
                return Music;
            IReferenceElement reference = element as IReferenceElement;
            if (reference != null)
                return Of(DataModule.ElementRepository.GetElement(reference.ReferencedId), depth + 1);
            int kinds = 0;
            // containers (also the sound lists with repeat): look at the files they hold
            IGeneralElementContainer container = element as IGeneralElementContainer;
            if (container != null)
            {
                foreach (IFileElement file in container.GetFileElements())
                    kinds |= file.SoundFileType == SoundFileType.Music ? Music : Effect;
                if (kinds != 0)
                    return kinds;
                foreach (IContainerElement inner in container.GetGeneralElements())
                    kinds |= Of(inner, depth + 1);
                if (kinds != 0)
                    return kinds;
            }
            IContainerElement wrapper = element as IContainerElement;
            if (wrapper != null && !ReferenceEquals(wrapper.InnerElement, element))
                return Of(wrapper.InnerElement, depth + 1);
            return kinds;
        }
    }
}
