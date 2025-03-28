﻿/*
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
   //! Base inflation-coupon pricer.
   /*! The main reason we can't use FloatingRateCouponPricer as the
       base is that it takes a FloatingRateCoupon which takes an
       InterestRateIndex and we need an inflation index (these are
       lagged).

       The basic inflation-specific thing that the pricer has to do
       is deal with different lags in the index and the option
       e.g. the option could look 3 months back and the index 2.

       We add the requirement that pricers do inverseCap/Floor-lets.
       These are cap/floor-lets as usually defined, i.e. pay out if
       underlying is above/below a strike.  The non-inverse (usual)
       versions are from a coupon point of view (a capped coupon has
       a maximum at the strike).

       We add the inverse prices so that conventional caps can be
       priced simply.
   */
   public class InflationCouponPricer
   {
      // Interface
      public virtual double swapletPrice() {return 0; }
      public virtual double swapletRate() { return 0; }
      public virtual double capletPrice(double effectiveCap) { return 0; }
      public virtual double capletRate(double effectiveCap) { return 0; }
      public virtual double floorletPrice(double effectiveFloor) { return 0; }
      public virtual double floorletRate(double effectiveFloor) { return 0; }
      public virtual void initialize(InflationCoupon i) {}
      protected Handle<YieldTermStructure> rateCurve_;
      protected Date paymentDate_;

   }

   //! base pricer for capped/floored YoY inflation coupons
   /*! \note this pricer can already do swaplets but to get
             volatility-dependent coupons you need the descendents.
   */
   public class YoYInflationCouponPricer : InflationCouponPricer
   {
      public YoYInflationCouponPricer(Handle<YoYOptionletVolatilitySurface> capletVol = null)
      {
         capletVol_ = capletVol ?? new Handle<YoYOptionletVolatilitySurface>();
      }

      public virtual Handle<YoYOptionletVolatilitySurface> capletVolatility()
      {
         return capletVol_;
      }

      public virtual void setCapletVolatility(Handle<YoYOptionletVolatilitySurface> capletVol)
      {
         Utils.QL_REQUIRE(!capletVol.empty(), () => "empty capletVol handle");

         capletVol_ = capletVol;
      }

      // InflationCouponPricer interface
      public override double swapletPrice()
      {
         double swapletPrice = adjustedFixing() * coupon_.accrualPeriod() * discount_;
         return gearing_ * swapletPrice + spreadLegValue_;
      }

      public override double swapletRate()
      {
         // This way we do not require the index to have
         // a yield curve, i.e. we do not get the problem
         // that a discounting-instrument-pricer is used
         // with a different yield curve
         return gearing_ * adjustedFixing() + spread_;
      }

      public override double capletPrice(double effectiveCap)
      {
         double capletPrice = optionletPrice(Option.Type.Call, effectiveCap);
         return gearing_ * capletPrice;
      }

      public override double capletRate(double effectiveCap)
      {
         return capletPrice(effectiveCap) / (coupon_.accrualPeriod() * discount_);
      }

      public override double floorletPrice(double effectiveFloor)
      {
         double floorletPrice = optionletPrice(Option.Type.Put, effectiveFloor);
         return gearing_ * floorletPrice;
      }
      public override double floorletRate(double effectiveFloor)
      {
         return floorletPrice(effectiveFloor) /
                (coupon_.accrualPeriod() * discount_);
      }

      public override void initialize(InflationCoupon coupon)
      {
         coupon_ = coupon as YoYInflationCoupon;
         gearing_ = coupon_.gearing();
         spread_ = coupon_.spread();
         paymentDate_ = coupon_.date();
         YoYInflationIndex y = (YoYInflationIndex)(coupon.index());
         rateCurve_ = y.yoyInflationTermStructure().link.nominalTermStructure();

         // past or future fixing is managed in YoYInflationIndex::fixing()
         // use yield curve from index (which sets discount)

         discount_ = 1.0;
         if (paymentDate_ > rateCurve_.link.referenceDate())
            discount_ = rateCurve_.link.discount(paymentDate_);

         spreadLegValue_ = spread_ * coupon_.accrualPeriod() * discount_;
      }

      //! car replace this if really required
      protected virtual double optionletPrice(Option.Type optionType, double effStrike)
      {

         Date fixingDate = coupon_.fixingDate();
         if (fixingDate <= Settings.Instance.evaluationDate())
         {
            // the amount is determined
            double a, b;
            if (optionType == Option.Type.Call)
            {
               a = coupon_.indexFixing();
               b = effStrike;
            }
            else
            {
               a = effStrike;
               b = coupon_.indexFixing();
            }
            return Math.Max(a - b, 0.0) * coupon_.accrualPeriod() * discount_;

         }
         else
         {
            // not yet determined, use Black/DD1/Bachelier/whatever from Impl
            Utils.QL_REQUIRE(!capletVolatility().empty(), () => "missing optionlet volatility");

            double stdDev = Math.Sqrt(capletVolatility().link.totalVariance(fixingDate, effStrike));

            double fixing = optionletPriceImp(optionType,
                                              effStrike,
                                              adjustedFixing(),
                                              stdDev);
            return fixing * coupon_.accrualPeriod() * discount_;

         }
      }

      //! usually only need implement this (of course they may need
      //! to re-implement initialize too ...)
      protected virtual double optionletPriceImp(Option.Type t, double strike,
                                                 double forward, double stdDev)
      {
         Utils.QL_FAIL("you must implement this to get a vol-dependent price");
         return 0;
      }

      protected virtual double adjustedFixing()
      {
         return adjustedFixing(null);
      }

      protected virtual double adjustedFixing(double? fixing)
      {
         if (fixing == null)
            fixing = coupon_.indexFixing();

         // no adjustment
         return fixing.Value;
      }

      //! data
      Handle<YoYOptionletVolatilitySurface> capletVol_;
      YoYInflationCoupon coupon_;
      double gearing_;
      double spread_;
      double discount_;
      double spreadLegValue_;
   }

   //! Black-formula pricer for capped/floored yoy inflation coupons
   public class BlackYoYInflationCouponPricer : YoYInflationCouponPricer
   {

      public BlackYoYInflationCouponPricer(Handle<YoYOptionletVolatilitySurface> capletVol)
         : base(capletVol)
      {}

      protected override double optionletPriceImp(Option.Type optionType, double effStrike,
                                                  double forward, double stdDev)
      {
         return Utils.blackFormula(optionType,
                                   effStrike,
                                   forward,
                                   stdDev);
      }

   }

   //! Unit-Displaced-Black-formula pricer for capped/floored yoy inflation coupons
   public class UnitDisplacedBlackYoYInflationCouponPricer : YoYInflationCouponPricer
   {
      public UnitDisplacedBlackYoYInflationCouponPricer(Handle<YoYOptionletVolatilitySurface> capletVol = null)
         : base(capletVol)
      {}

      protected override double optionletPriceImp(Option.Type optionType, double effStrike,
                                                  double forward, double stdDev)
      {

         return Utils.blackFormula(optionType,
                                   effStrike + 1.0,
                                   forward + 1.0,
                                   stdDev);
      }
   }

   //! Bachelier-formula pricer for capped/floored yoy inflation coupons
   public class BachelierYoYInflationCouponPricer : YoYInflationCouponPricer
   {
      public BachelierYoYInflationCouponPricer(Handle<YoYOptionletVolatilitySurface> capletVol = null)
         : base(capletVol)
      {}

      protected override double optionletPriceImp(Option.Type optionType, double effStrike,
                                                  double forward, double stdDev)
      {
         return Utils.bachelierBlackFormula(optionType,
                                            effStrike,
                                            forward,
                                            stdDev);
      }
   }
}
