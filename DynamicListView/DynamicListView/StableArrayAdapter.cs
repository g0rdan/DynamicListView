using System;
using System.Collections.Generic;
using Android.Content;
using Android.Widget;

namespace DynamicListView
{
    public class StableArrayAdapter : ArrayAdapter<string>
    {
        const int INVALID_ID = -1;
        Dictionary<string, int> mIdMap = new Dictionary<string, int> ();

        public StableArrayAdapter (Context context, int textViewResourceId, List<string> objects) : base (context, textViewResourceId, objects)
        {
            for (int i = 0; i < objects.Count; ++i) {
                mIdMap.Add (objects[i], i);
            }
        }

        public override long GetItemId (int position)
        {
            if (position < 0 || position >= mIdMap.Count) 
                return INVALID_ID;
            
            string item = GetItem (position);
            return mIdMap[item];
        }

        public override bool HasStableIds {
            get {
                return true;
            }
        }
    }
}

