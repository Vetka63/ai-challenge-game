using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AsciiArena
{
    public sealed partial class ArenaHud
    {
        private Image heroPreview;
        private TextMeshProUGUI heroPreviewName;
        private void UpdateHeroPreview()
        {
            if (heroPreview == null)
            {
                var obj=new GameObject("Menu sprite hero",typeof(RectTransform),typeof(Image));
                var r=obj.GetComponent<RectTransform>(); r.SetParent(menuArt,false);
                r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
                r.anchoredPosition=new Vector2(-25,-30);r.sizeDelta=new Vector2(245,285);
                heroPreview=obj.GetComponent<Image>();heroPreview.preserveAspect=true;heroPreview.raycastTarget=false;
                heroPreviewName=MenuText(menuArt,"",new Vector2(20,-437),new Vector2(490,30),20,MenuInk);
                heroPreviewName.alignment=TextAlignmentOptions.Center;
            }
            var identity=session.Home?session.Profile.selectedHero:simulation.HeroIdentity;
            heroPreview.sprite=HeroSprites.Get(identity);heroPreview.enabled=heroPreview.sprite!=null;
            var old=menuArt.Find("Menu ASCII hero");if(old!=null)old.gameObject.SetActive(identity==HeroIdentity.Ascii);
            heroPreviewName.text=HeroRoster.Name(identity);
        }
    }
}
