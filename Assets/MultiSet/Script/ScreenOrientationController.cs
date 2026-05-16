using UnityEngine;

namespace MultiSet
{
	public class ScreenOrientationController : MonoBehaviour
	{
		public enum Orientation
		{
			Portrait,
			Landscape,
			AutoRotate
		}

		public Orientation sceneOrientation;

		private void Start()
		{
			switch (sceneOrientation)
			{
			case Orientation.Portrait:
				Screen.orientation = (ScreenOrientation)1;
				break;
			case Orientation.Landscape:
				Screen.orientation = (ScreenOrientation)3;
				break;
			case Orientation.AutoRotate:
				Screen.orientation = (ScreenOrientation)5;
				Screen.autorotateToPortrait = true;
				Screen.autorotateToPortraitUpsideDown = true;
				Screen.autorotateToLandscapeLeft = true;
				Screen.autorotateToLandscapeRight = true;
				break;
			}
		}
	}
}
