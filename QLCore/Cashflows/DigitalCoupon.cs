/*
 Copyright (C) 2020 Jean-Camille Tournier (mail@tournierjc.fr)

 This file is part of QLCore Project https://github.com/OpenDerivatives/QLCore

 QLCore is free software: you can redistribute it and/or modify it
 under the terms of the QLCore and QLNet license. You should have received a
 copy of the license along with this program; if not, license is
 available at https://github.com/OpenDerivatives/QLCore/LICENSE.

 QLCore is a forked of QLNet which is a based on QuantLib, a free-software/open-source
 library for financial quantitative analysts and developers - http://quantlib.org/
 The QuantLib license is available online at http://quantlib.org/license.shtml and the
 QLNet license is available online at https://github.com/amaggiulli/QLNet/blob/develop/LICENSE.

 This program is distributed in the hope that it will be useful, but WITHOUT
 ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 FOR A PARTICAR PURPOSE. See the license for more details.
*/

using System;

namespace QLCore
{

   //! Digital-payoff coupon
   //    ! Implementation of a floating-rate coupon with digital call/put option.
   //        Payoffs:
   //        - Coupon with cash-or-nothing Digital Call
   //          rate + csi * payoffRate * Heaviside(rate-strike)
   //        - Coupon with cash-or-nothing Digital Put
   //          rate + csi * payoffRate * Heaviside(strike-rate)
   //        where csi=+1 or csi=-1.
   //        - Coupon with asset-or-nothing Digital Call
   //          rate + csi * rate * Heaviside(rate-strike)
   //        - Coupon with asset-or-nothing Digital Put
   //          rate + csi * rate * Heaviside(strike-rate)
   //        where csi=+1 or csi=-1.
   //        The evaluation of the coupon is made using the call/put spread
   //        replication method.
   //
   //    ! \ingroup instruments
   //
   //        \test
   //        - the correctness of the returned value in case of Asset-or-nothing
   //          embedded option is tested by pricing the digital option with
   //          Cox-Rubinstein formula.
   //        - the correctness of the returned value in case of deep-in-the-money
   //          Asset-or-nothing embedded option is tested vs the expected values of
   //          coupon and option.
   //        - the correctness of the returned value in case of deep-out-of-the-money
   //          Asset-or-nothing embedded option is tested vs the expected values of
   //          coupon and option.
   //        - the correctness of the returned value in case of Cash-or-nothing
   //          embedded option is tested by pricing the digital option with
   //          Reiner-Rubinstein formula.
   //        - the correctness of the returned value in case of deep-in-the-money
   //          Cash-or-nothing embedded option is tested vs the expected values of
   //          coupon and option.
   //        - the correctness of the returned value in case of deep-out-of-the-money
   //          Cash-or-nothing embedded option is tested vs the expected values of
   //          coupon and option.
   //        - the correctness of the returned value is tested checking the correctness
   //          of the call-put parity relation.
   //        - the correctness of the returned value is tested by the relationship
   //          between prices in case of different replication types.
   //
   public class DigitalCoupon : FloatingRateCoupon
   {
      // need by CashFlowVectors
      public DigitalCoupon() { }

      //! Constructors
      //! general constructor
      public DigitalCoupon(FloatingRateCoupon underlying,
                           double? callStrike = null,
                           Position.Type callPosition = Position.Type.Long,
                           bool isCallATMIncluded = false,
                           double? callDigitalPayoff = null,
                           double? putStrike = null,
                           Position.Type putPosition = Position.Type.Long,
                           bool isPutATMIncluded = false,
                           double? putDigitalPayoff = null,
                           DigitalReplication replication = null)
      : base(underlying.date(), underlying.nominal(), underlying.accrualStartDate(), underlying.accrualEndDate(), underlying.fixingDays, underlying.index(), underlying.gearing(), underlying.spread(), underlying.referencePeriodStart, underlying.referencePeriodEnd, underlying.dayCounter(), underlying.isInArrears())
      {
         if (replication == null)
            replication = new DigitalReplication();

         underlying_ = underlying;
         callCsi_ = 0.0;
         putCsi_ = 0.0;
         isCallATMIncluded_ = isCallATMIncluded;
         isPutATMIncluded_ = isPutATMIncluded;
         isCallCashOrNothing_ = false;
         isPutCashOrNothing_ = false;
         callLeftEps_ = replication.gap() / 2.0;
         callRightEps_ = replication.gap() / 2.0;
         putLeftEps_ = replication.gap() / 2.0;
         putRightEps_ = replication.gap() / 2.0;
         hasPutStrike_ = false;
         hasCallStrike_ = false;
         replicationType_ = replication.replicationType();


         Utils.QL_REQUIRE(replication.gap() > 0.0, () => "Non positive epsilon not allowed");

         if (putStrike == null)
            Utils.QL_REQUIRE(putDigitalPayoff == null, () => "Put Cash rate non allowed if put strike is null");

         if (callStrike == null)
            Utils.QL_REQUIRE(callDigitalPayoff == null, () => "Call Cash rate non allowed if call strike is null");

         if (callStrike != null)
         {
            Utils.QL_REQUIRE(callStrike >= 0.0, () => "negative call strike not allowed");

            hasCallStrike_ = true;
            callStrike_ = callStrike.GetValueOrDefault();
            Utils.QL_REQUIRE(callStrike_ >= replication.gap() / 2.0, () => "call strike < eps/2");

            switch (callPosition)
            {
               case Position.Type.Long:
                  callCsi_ = 1.0;
                  break;
               case Position.Type.Short:
                  callCsi_ = -1.0;
                  break;
               default:
                  Utils.QL_FAIL("unsupported position type");
                  break;
            }
            if (callDigitalPayoff != null)
            {
               callDigitalPayoff_ = callDigitalPayoff.GetValueOrDefault();
               isCallCashOrNothing_ = true;
            }
         }
         if (putStrike != null)
         {
            Utils.QL_REQUIRE(putStrike >= 0.0, () => "negative put strike not allowed");
            hasPutStrike_ = true;
            putStrike_ = putStrike.GetValueOrDefault();
            switch (putPosition)
            {
               case Position.Type.Long:
                  putCsi_ = 1.0;
                  break;
               case Position.Type.Short:
                  putCsi_ = -1.0;
                  break;
               default:
                  Utils.QL_FAIL("unsupported position type");
                  break;
            }
            if (putDigitalPayoff != null)
            {
               putDigitalPayoff_ = putDigitalPayoff.GetValueOrDefault();
               isPutCashOrNothing_ = true;
            }
         }

         switch (replicationType_)
         {
            case Replication.Type.Central:
               // do nothing
               break;
            case Replication.Type.Sub:
               if (hasCallStrike_)
               {
                  switch (callPosition)
                  {
                     case Position.Type.Long:
                        callLeftEps_ = 0.0;
                        callRightEps_ = replication.gap();
                        break;
                     case Position.Type.Short:
                        callLeftEps_ = replication.gap();
                        callRightEps_ = 0.0;
                        break;
                     default:
                        Utils.QL_FAIL("unsupported position type");
                        break;
                  }
               }
               if (hasPutStrike_)
               {
                  switch (putPosition)
                  {
                     case Position.Type.Long:
                        putLeftEps_ = replication.gap();
                        putRightEps_ = 0.0;
                        break;
                     case Position.Type.Short:
                        putLeftEps_ = 0.0;
                        putRightEps_ = replication.gap();
                        break;
                     default:
                        Utils.QL_FAIL("unsupported position type");
                        break;
                  }
               }
               break;
            case Replication.Type.Super:
               if (hasCallStrike_)
               {
                  switch (callPosition)
                  {
                     case Position.Type.Long:
                        callLeftEps_ = replication.gap();
                        callRightEps_ = 0.0;
                        break;
                     case Position.Type.Short:
                        callLeftEps_ = 0.0;
                        callRightEps_ = replication.gap();
                        break;
                     default:
                        Utils.QL_FAIL("unsupported position type");
                        break;
                  }
               }
               if (hasPutStrike_)
               {
                  switch (putPosition)
                  {
                     case Position.Type.Long:
                        putLeftEps_ = 0.0;
                        putRightEps_ = replication.gap();
                        break;
                     case Position.Type.Short:
                        putLeftEps_ = replication.gap();
                        putRightEps_ = 0.0;
                        break;
                     default:
                        Utils.QL_FAIL("unsupported position type");
                        break;
                  }
               }
               break;
            default:
               Utils.QL_FAIL("unsupported position type");
               break;
         }
      }

      // Coupon interface
      public override double rate()
      {

         Utils.QL_REQUIRE(underlying_.pricer() != null, () => "pricer not set");

         Date fixingDate = underlying_.fixingDate();
         Date today = Settings.Instance.evaluationDate();
         bool enforceTodaysHistoricFixings = Settings.Instance.enforcesTodaysHistoricFixings;
         double underlyingRate = underlying_.rate();
         if (fixingDate < today || ((fixingDate == today) && enforceTodaysHistoricFixings))
         {
            // must have been fixed
            return underlyingRate + callCsi_ * callPayoff() + putCsi_ * putPayoff();
         }
         if (fixingDate == today)
         {
            // might have been fixed
            double? pastFixing = (underlying_.index()).timeSeries()[fixingDate];
            if (pastFixing != null)
            {
               return underlyingRate + callCsi_ * callPayoff() + putCsi_ * putPayoff();
            }
            else
               return underlyingRate + callCsi_ * callOptionRate() + putCsi_ * putOptionRate();
         }
         return underlyingRate + callCsi_ * callOptionRate() + putCsi_ * putOptionRate();
      }
      public override double convexityAdjustment()
      {
         return underlying_.convexityAdjustment();
      }
      // Digital inspectors
      public double? callStrike()
      {
         if (hasCall())
            return callStrike_;

         return null;
      }
      public double? putStrike()
      {
         if (hasPut())
            return putStrike_;

         return null;
      }
      public double? callDigitalPayoff()
      {
         if (isCallCashOrNothing_)
            return callDigitalPayoff_;

         return null;
      }
      public double? putDigitalPayoff()
      {
         if (isPutCashOrNothing_)
            return putDigitalPayoff_;

         return null;
      }
      public bool hasPut()
      {
         return hasPutStrike_;
      }
      public bool hasCall()
      {
         return hasCallStrike_;
      }
      public bool hasCollar()
      {
         return (hasCallStrike_ && hasPutStrike_);
      }
      public bool isLongPut()
      {
         return putCsi_.IsEqual(1.0);
      }
      public bool isLongCall()
      {
         return callCsi_.IsEqual(1.0);
      }
      public FloatingRateCoupon underlying()
      {
         return underlying_;
      }
      //        ! Returns the call option rate
      //           (multiplied by: nominal*accrualperiod*discount is the NPV of the option)
      //
      public double callOptionRate()
      {

         double callOptionRate = 0.0;
         if (hasCallStrike_)
         {
            // Step function
            callOptionRate = isCallCashOrNothing_ ? callDigitalPayoff_ : callStrike_;
            CappedFlooredCoupon next = new CappedFlooredCoupon(underlying_, callStrike_ + callRightEps_);
            CappedFlooredCoupon previous = new CappedFlooredCoupon(underlying_, callStrike_ - callLeftEps_);
            callOptionRate *= (next.rate() - previous.rate()) / (callLeftEps_ + callRightEps_);
            if (!isCallCashOrNothing_)
            {
               // Call
               CappedFlooredCoupon atStrike = new CappedFlooredCoupon(underlying_, callStrike_);
               double call = underlying_.rate() - atStrike.rate();
               // Sum up
               callOptionRate += call;
            }
         }
         return callOptionRate;
      }
      //        ! Returns the put option rate
      //           (multiplied by: nominal*accrualperiod*discount is the NPV of the option)
      //
      public double putOptionRate()
      {

         double putOptionRate = 0.0;
         if (hasPutStrike_)
         {
            // Step function
            putOptionRate = isPutCashOrNothing_ ? putDigitalPayoff_ : putStrike_;
            CappedFlooredCoupon next = new CappedFlooredCoupon(underlying_, null, putStrike_ + putRightEps_);
            CappedFlooredCoupon previous = new CappedFlooredCoupon(underlying_, null, putStrike_ - putLeftEps_);
            putOptionRate *= (next.rate() - previous.rate()) / (putLeftEps_ + putRightEps_);
            if (!isPutCashOrNothing_)
            {
               // Put
               CappedFlooredCoupon atStrike = new CappedFlooredCoupon(underlying_, null, putStrike_);
               double put = -underlying_.rate() + atStrike.rate();
               // Sum up
               putOptionRate -= put;
            }
         }
         return putOptionRate;
      }

      public override void setPricer(FloatingRateCouponPricer pricer)
      {
         pricer_ = pricer;
         underlying_.setPricer(pricer);
      }

      // Factory - for Leg generators
      public virtual CashFlow factory(FloatingRateCoupon underlying, double? callStrike, Position.Type callPosition, bool isCallATMIncluded, double? callDigitalPayoff, double? putStrike, Position.Type putPosition, bool isPutATMIncluded, double? putDigitalPayoff, DigitalReplication replication)
      {
         return new DigitalCoupon(underlying, callStrike, callPosition, isCallATMIncluded, callDigitalPayoff, putStrike, putPosition, isPutATMIncluded, putDigitalPayoff, replication);
      }

      // Data members
      protected FloatingRateCoupon underlying_;
      //! strike rate for the the call option
      protected double callStrike_;
      //! strike rate for the the put option
      protected double putStrike_;
      //! multiplicative factor of call payoff
      protected double callCsi_;
      //! multiplicative factor of put payoff
      protected double putCsi_;
      //! inclusion flag og the call payoff if the call option ends at-the-money
      protected bool isCallATMIncluded_;
      //! inclusion flag og the put payoff if the put option ends at-the-money
      protected bool isPutATMIncluded_;
      //! digital call option type: if true, cash-or-nothing, if false asset-or-nothing
      protected bool isCallCashOrNothing_;
      //! digital put option type: if true, cash-or-nothing, if false asset-or-nothing
      protected bool isPutCashOrNothing_;
      //! digital call option payoff rate, if any
      protected double callDigitalPayoff_;
      //! digital put option payoff rate, if any
      protected double putDigitalPayoff_;
      //! the left and right gaps applied in payoff replication for call
      protected double callLeftEps_;
      protected double callRightEps_;
      //! the left and right gaps applied in payoff replication for puf
      protected double putLeftEps_;
      protected double putRightEps_;
      //!
      protected bool hasPutStrike_;
      protected bool hasCallStrike_;
      //! Type of replication
      protected Replication.Type replicationType_;

      private double callPayoff()
      {
         // to use only if index has fixed
         double payoff = 0.0;
         if (hasCallStrike_)
         {
            double underlyingRate = underlying_.rate();
            if ((underlyingRate - callStrike_) > 1.0e-16)
            {
               payoff = isCallCashOrNothing_ ? callDigitalPayoff_ : underlyingRate;
            }
            else
            {
               if (isCallATMIncluded_ && Math.Abs(callStrike_ - underlyingRate) <= 1.0e-16)
                  payoff = isCallCashOrNothing_ ? callDigitalPayoff_ : underlyingRate;
            }
         }
         return payoff;
      }
      private double putPayoff()
      {
         // to use only if index has fixed
         double payoff = 0.0;
         if (hasPutStrike_)
         {
            double underlyingRate = underlying_.rate();
            if ((putStrike_ - underlyingRate) > 1.0e-16)
            {
               payoff = isPutCashOrNothing_ ? putDigitalPayoff_ : underlyingRate;
            }
            else
            {
               // putStrike_ <= underlyingRate
               if (isPutATMIncluded_)
               {
                  if (Math.Abs(putStrike_ - underlyingRate) <= 1.0e-16)
                     payoff = isPutCashOrNothing_ ? putDigitalPayoff_ : underlyingRate;
               }
            }
         }
         return payoff;
      }

   }

}
