using System;
using System.Collections.Generic;
using Android.Animation;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;

namespace DynamicListView
{
    public class DynamicListView : ListView, ViewTreeObserver.IOnPreDrawListener, ValueAnimator.IAnimatorUpdateListener, ITypeEvaluator
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

        public int INVALID_ID = -1;

        public long mAboveItemId = -1;
        public long mMobileItemId = -1;
        public long mBelowItemId = -1;

        public BitmapDrawable mHoverCell;
        Rect mHoverCellCurrentBounds;
        Rect mHoverCellOriginalBounds;


        const int INVALID_POINTER_ID = -1;
        int mActivePointerId = INVALID_POINTER_ID;

        bool mIsWaitingForScrollFinish = false;
        int mScrollState = 0; //OnScrollListener.SCROLL_STATE_IDLE;

        #region ViewTreeObserver
        ViewTreeObserver observer;
        long observeSwitchItemID;
        int observeSwitchViewStartTop;
        int observeDeltaY;
        #endregion

        #region touchEventsEnded()
        public View touchEventsEndedMobileView;
        #endregion

        #region Constructors
        public DynamicListView (Context context) : base (context)
        {
            Init (context);
        }

        public DynamicListView (Context context, IAttributeSet attrs, int defStyle) : base (context, attrs, defStyle)
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
                mDownX = (int)e.GetX ();
                mDownY = (int)e.GetY ();
                mActivePointerId = e.GetPointerId (0);
                break;
            case MotionEventActions.Move:
                if (mActivePointerId == INVALID_POINTER_ID)
                    break;

                int pointerIndex = e.FindPointerIndex (mActivePointerId);
                mLastEventY = (int)e.GetY (pointerIndex);
                int deltaY = mLastEventY - mDownY;

                if (mCellIsMobile) {
                    mHoverCellCurrentBounds.OffsetTo (mHoverCellOriginalBounds.Left, mHoverCellOriginalBounds.Top + deltaY + mTotalOffset);
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
                int pointerId = e.GetPointerId (pointerIndex);
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



        /**
         * Resets all the appropriate fields to a default state while also animating
         * the hover cell back to its correct location.
         */
        void touchEventsEnded ()
        {
            touchEventsEndedMobileView = getViewForID (mMobileItemId);
            if (mCellIsMobile || mIsWaitingForScrollFinish) {
                mCellIsMobile = false;
                mIsWaitingForScrollFinish = false;
                mIsMobileScrolling = false;
                mActivePointerId = INVALID_POINTER_ID;

                // If the autoscroller has not completed scrolling, we need to wait for it to
                // finish in order to determine the final location of where the hover cell
                // should be animated to.
                if (mScrollState != 0) {//OnScrollListener.SCROLL_STATE_IDLE) {
                    mIsWaitingForScrollFinish = true;
                    return;
                }

                mHoverCellCurrentBounds.OffsetTo (mHoverCellOriginalBounds.Left, touchEventsEndedMobileView.Top);
                ObjectAnimator hoverViewAnimator = ObjectAnimator.OfObject (mHoverCell, "bounds", this, mHoverCellCurrentBounds);
                hoverViewAnimator.AddUpdateListener (this);
                hoverViewAnimator.AddListener (new MyAnimatorListenerAdapter(this));
                hoverViewAnimator.Start ();
            }
        }

        /**
         * This method determines whether the hover cell has been shifted far enough
         * to invoke a cell swap. If so, then the respective cell swap candidate is
         * determined and the data set is changed. Upon posting a notification of the
         * data set change, a layout is invoked to place the cells in the right place.
         * Using a ViewTreeObserver and a corresponding OnPreDrawListener, we can
         * offset the cell being swapped to where it previously was and then animate it to
         * its new position.
         */
        void handleCellSwitch ()
        {
            observeDeltaY = mLastEventY - mDownY;
            int deltaYTotal = mHoverCellOriginalBounds.Top + mTotalOffset + observeDeltaY;

            View belowView = getViewForID (mBelowItemId);
            View mobileView = getViewForID (mMobileItemId);
            View aboveView = getViewForID (mAboveItemId);

            bool isBelow = (belowView != null) && (deltaYTotal > belowView.Top);
            bool isAbove = (aboveView != null) && (deltaYTotal < aboveView.Top);

            if (isBelow || isAbove) {

                observeSwitchItemID = isBelow ? mBelowItemId : mAboveItemId;
                View switchView = isBelow ? belowView : aboveView;
                int originalItem = GetPositionForView (mobileView);

                if (switchView == null) {
                    updateNeighborViewsForID (mMobileItemId);
                    return;
                }

                swapElements (mCheeseList, originalItem, GetPositionForView (switchView));

                ((BaseAdapter)Adapter).NotifyDataSetChanged ();

                mDownY = mLastEventY;

                observeSwitchViewStartTop = switchView.Top;

                mobileView.Visibility = ViewStates.Visible;
                switchView.Visibility = ViewStates.Invisible;

                updateNeighborViewsForID (mMobileItemId);

                observer = ViewTreeObserver;
                observer.AddOnPreDrawListener (this);
            }
        }

        void swapElements (List<string> arrayList, int indexOne, int indexTwo)
        {
            var temp = arrayList[indexOne];
            arrayList[indexOne] = arrayList[indexTwo];
            arrayList[indexTwo] = temp;
        }

        void handleMobileCellScroll ()
        {
            throw new NotImplementedException ();
        }

        #region Interfaces implementation
        public bool OnPreDraw ()
        {
            observer.RemoveOnPreDrawListener (this);

            View switchView = getViewForID (observeSwitchItemID);

            mTotalOffset += observeDeltaY;

            int switchViewNewTop = switchView.Top;
            int delta = observeSwitchViewStartTop - switchViewNewTop;

            switchView.TranslationY = delta;

            ObjectAnimator animator = ObjectAnimator.OfFloat (switchView, nameof (this.TranslationY), 0); //TODO непонтяно с TranslationY
            animator.SetDuration (MOVE_DURATION);
            animator.Start ();

            return true;
        }

        public void OnAnimationUpdate (ValueAnimator animation)
        {
            Invalidate ();
        }

        public Java.Lang.Object Evaluate (float fraction, Java.Lang.Object startValue, Java.Lang.Object endValue)
        {
            var startValueRect = (Rect)startValue;
            var endValueRect = (Rect)endValue;

            return new Rect (interpolate (startValueRect.Left, endValueRect.Left, fraction),
                    interpolate (startValueRect.Top, endValueRect.Top, fraction),
                    interpolate (startValueRect.Right, endValueRect.Right, fraction),
                    interpolate (startValueRect.Bottom, endValueRect.Bottom, fraction));
        }

        #endregion

        int interpolate (int start, int end, float fraction)
        {
            return (int)(start + fraction * (end - start));
        }
    }

    public class MyAnimatorListenerAdapter : AnimatorListenerAdapter
    {
        DynamicListView _dynamicListView;

        public MyAnimatorListenerAdapter (DynamicListView dynamicListView)
        {
            _dynamicListView = dynamicListView;
        }

        public override void OnAnimationStart (Animator animation)
        {
            _dynamicListView.Enabled = false;
        }

        public override void OnAnimationEnd (Animator animation)
        {
            base.OnAnimationEnd (animation);

            _dynamicListView.mAboveItemId = _dynamicListView.INVALID_ID;
            _dynamicListView.mMobileItemId = _dynamicListView.INVALID_ID;
            _dynamicListView.mBelowItemId = _dynamicListView.INVALID_ID;
            _dynamicListView.touchEventsEndedMobileView.Visibility = ViewStates.Visible;
            _dynamicListView.mHoverCell = null;
            _dynamicListView.Enabled = true;
            _dynamicListView.Invalidate ();
        }
    }

    //public class MyTypeConverter : TypeConverter
    //{ 
    //    public MyTypeConverter (Class fromClass, Class toClass) : base (fromClass, toClass)
    //    {
            
    //    }

    //    public override Java.Lang.Object Convert (Java.Lang.Object value)
    //    {
    //        throw new NotImplementedException ();
    //    }

    //    public int interpolate (int start, int end, float fraction)
    //    {
    //        return (int)(start + fraction * (end - start));
    //    }
    //}
}

