using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
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

        /**
         * Creates the hover cell with the appropriate bitmap and of appropriate
         * size. The hover cell's BitmapDrawable is drawn on top of the bitmap every
         * single time an invalidate call is made.
         */
        BitmapDrawable getAndAddHoverView (View v)
        {
            int w = v.Width;
            int h = v.Height;
            int top = v.Top;
            int left = v.Left;

            Bitmap b = getBitmapWithBorder (v);

            BitmapDrawable drawable = new BitmapDrawable (Resources, b);

            mHoverCellOriginalBounds = new Rect (left, top, left + w, top + h);
            mHoverCellCurrentBounds = new Rect (mHoverCellOriginalBounds);

            drawable.Bounds = mHoverCellCurrentBounds; // maybe SetBounds()... 

            return drawable;
        }

        /** Draws a black border over the screenshot of the view passed in. */
        Bitmap getBitmapWithBorder (View v)
        {
            Bitmap bitmap = getBitmapFromView (v);
            Canvas can = new Canvas (bitmap);

            Rect rect = new Rect (0, 0, bitmap.Width, bitmap.Height);

            Paint paint = new Paint ();
            paint.SetStyle (Paint.Style.Stroke);
            paint.StrokeWidth = LINE_THICKNESS;
            paint.Color = Color.Black;

            can.DrawBitmap (bitmap, 0, 0, null);
            can.DrawRect (rect, paint);

            return bitmap;
        }

        /** Returns a bitmap showing a screenshot of the view passed in. */
        Bitmap getBitmapFromView (View v)
        {
            Bitmap bitmap = Bitmap.CreateBitmap (v.Width, v.Height, Bitmap.Config.Argb8888);
            Canvas canvas = new Canvas (bitmap);
            v.Draw (canvas);
            return bitmap;
        }

        /**
         * Stores a reference to the views above and below the item currently
         * corresponding to the hover cell. It is important to note that if this
         * item is either at the top or bottom of the list, mAboveItemId or mBelowItemId
         * may be invalid.
         */
        void updateNeighborViewsForID (long itemID)
        {
            int position = getPositionForID (itemID);
            StableArrayAdapter adapter = (StableArrayAdapter)Adapter;
            mAboveItemId = adapter.GetItemId (position - 1);
            mBelowItemId = adapter.GetItemId (position + 1);
        }

        /** Retrieves the view in the list corresponding to itemID */
        public View getViewForID (long itemID)
        {
            int firstVisiblePosition = FirstVisiblePosition;
            StableArrayAdapter adapter = (StableArrayAdapter)Adapter;
            for (int i = 0; i < ChildCount; i++) {
                View v = GetChildAt (i);
                int position = firstVisiblePosition + i;
                long id = adapter.GetItemId (position);
                if (id == itemID) {
                    return v;
                }
            }
            return null;
        }

        /** Retrieves the position in the list corresponding to itemID */
        public int getPositionForID (long itemID)
        {
            View v = getViewForID (itemID);
            if (v == null) {
                return -1;
            } else {
                return GetPositionForView (v);
            }
        }

        protected override void DispatchDraw (Canvas canvas)
        {
            base.DispatchDraw (canvas);
            if (mHoverCell != null) {
                mHoverCell.Draw (canvas);
            }
        }

        public override bool OnTouchEvent (MotionEvent e)
        {
            switch (e.Action & MotionEventActions.Mask) {
            case MotionEventActions.Down:
                mDownX = (int)e.GetX();
                mDownY = (int)e.GetY();
                mActivePointerId = e.GetPointerId(0);
                break;
            case MotionEventActions.Move:
                if (mActivePointerId == INVALID_POINTER_ID)
                    break;

                int pointerIndex = e.FindPointerIndex(mActivePointerId);
                mLastEventY = (int) e.GetY(pointerIndex);
                int deltaY = mLastEventY - mDownY;

                if (mCellIsMobile) {
                    mHoverCellCurrentBounds.OffsetTo(mHoverCellOriginalBounds.Left, mHoverCellOriginalBounds.Top + deltaY + mTotalOffset);
                    mHoverCell.Bounds = mHoverCellCurrentBounds; // maybe SetBounds()
                    Invalidate ();
                    handleCellSwitch ();
                    mIsMobileScrolling = false;
                    handleMobileCellScroll ();
                    return false;
                }
                break;
            case MotionEventActions.Up:
                touchEventsEnded ();
                break;
            case MotionEventActions.Cancel:
                touchEventsCancelled ();
                break;
            case MotionEventActions.PointerUp:
                /* If a multitouch event took place and the original touch dictating
                 * the movement of the hover cell has ended, then the dragging event
                 * ends and the hover cell is animated to its corresponding position
                 * in the listview. */
                pointerIndex = (int)(e.Action & MotionEventActions.PointerIndexMask) >> (int)MotionEventActions.PointerIndexShift;
                int pointerId = e.GetPointerId(pointerIndex);
                if (pointerId == mActivePointerId)
                    touchEventsEnded ();
                break;
            default:
                break;
            }

            return base.OnTouchEvent (e);
        }

        void touchEventsCancelled ()
        {
            throw new NotImplementedException ();
        }

        void touchEventsEnded ()
        {
            throw new NotImplementedException ();
        }

        void handleCellSwitch ()
        {
            throw new NotImplementedException ();
        }

        void handleMobileCellScroll ()
        {
            throw new NotImplementedException ();
        }
   }
}

