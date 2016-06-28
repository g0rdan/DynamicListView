using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Widget;

namespace DynamicListView
{
    public class DynamicListView : ListView
    {
        const int SMOOTH_SCROLL_AMOUNT_AT_EDGE = 15;
        const int MOVE_DURATION = 150;
        const int LINE_THICKNESS = 15;

        public List<string> mCheeseList;

        int mLastEventY = -1;

        int mDownY = -1;
        int mDownX = -1;

        int mTotalOffset = 0;

        bool mCellIsMobile;
        bool mIsMobileScrolling;
        int mSmoothScrollAmountAtEdge = 0;

        const int INVALID_ID = -1;
        long mAboveItemId = INVALID_ID;
        long mMobileItemId = INVALID_ID;
        long mBelowItemId = INVALID_ID;

        BitmapDrawable mHoverCell;
        Rect mHoverCellCurrentBounds;
        Rect mHoverCellOriginalBounds;

        const int INVALID_POINTER_ID = -1;
        int mActivePointerId = INVALID_POINTER_ID;

        bool mIsWaitingForScrollFinish = false;
        int mScrollState = 0; //OnScrollListener.SCROLL_STATE_IDLE;

        #region Constructors
        public DynamicListView (Context context) : base (context)
        {
            Init (context);
        }

        public DynamicListView (Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
            Init (context);
        }

        public DynamicListView (Context context, IAttributeSet attrs) : base (context, attrs)
        {
            Init (context);
        }
        #endregion


        void Init (Context context)
        {
            throw new NotImplementedException ();
        }
   }
}

