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
using System.Collections.Generic;

namespace QLCore
{
   public class OvernightIndexedCouponPricer : FloatingRateCouponPricer
   {
      private OvernightIndexedCoupon coupon_;

      public override void initialize(FloatingRateCoupon coupon)
      {
         coupon_ = coupon as OvernightIndexedCoupon;
         Utils.QL_REQUIRE(coupon_ != null, () => "wrong coupon type");
      }

      public override double swapletRate()
      {
         OvernightIndex index = coupon_.index() as OvernightIndex;

         List<Date> fixingDates = coupon_.fixingDates();
         List<double> dt = coupon_.dt();

         int n = dt.Count;
         int i = 0;

         double compoundFactor = 1.0;

         // already fixed part
         Date today = Settings.Instance.evaluationDate();
         while (fixingDates[i] < today && i < n)
         {
            // rate must have been fixed
            double? pastFixing = index.timeSeries()[fixingDates[i]];

            Utils.QL_REQUIRE(pastFixing != null, () => "Missing " + index.name() + " fixing for " + fixingDates[i].ToString());

            compoundFactor *= (1.0 + pastFixing.GetValueOrDefault() * dt[i]);
            ++i;
         }

         // today is a border case
         if (fixingDates[i] == today && i < n)
         {
            // might have been fixed
            try
            {
               double? pastFixing = index.timeSeries()[fixingDates[i]];

               if (pastFixing != null)
               {
                  compoundFactor *= (1.0 + pastFixing.GetValueOrDefault() * dt[i]);
                  ++i;
               }
               else
               {
                  // fall through and forecast
               }
            }
            catch (Exception)
            {
               // fall through and forecast
            }
         }

         // forward part using telescopic property in order
         // to avoid the evaluation of multiple forward fixings
         if (i < n)
         {
            Handle<YieldTermStructure> curve = index.forwardingTermStructure();
            Utils.QL_REQUIRE(!curve.empty(), () => "null term structure set to this instance of" + index.name());

            List<Date> dates = coupon_.valueDates();
            double startDiscount = curve.link.discount(dates[i]);
            double endDiscount = curve.link.discount(dates[n]);

            compoundFactor *= startDiscount / endDiscount;
         }

         double rate = (compoundFactor - 1.0) / coupon_.accrualPeriod();
         return coupon_.gearing() * rate + coupon_.spread();
      }

      public override double swapletPrice() { Utils.QL_FAIL("swapletPrice not available"); return 0; }
      public override double capletPrice(double d) { Utils.QL_FAIL("capletPrice not available"); return 0; }
      public override double capletRate(double d) { Utils.QL_FAIL("capletRate not available"); return 0; }
      public override double floorletPrice(double d) { Utils.QL_FAIL("floorletPrice not available"); return 0; }
      public override double floorletRate(double d) { Utils.QL_FAIL("floorletRate not available"); return 0; }
      public override double optionletPrice(Option.Type type, double effectiveStrike) { Utils.QL_FAIL("floorletRate not available"); return 0; }
   }

   public class OvernightIndexedCoupon : FloatingRateCoupon
   {
      public OvernightIndexedCoupon(
         Date paymentDate,
         double nominal,
         Date startDate,
         Date endDate,
         OvernightIndex overnightIndex,
         double gearing = 1.0,
         double spread = 0.0,
         Date refPeriodStart = null,
         Date refPeriodEnd = null,
         DayCounter dayCounter = null)
         : base(paymentDate, nominal, startDate, endDate,
                overnightIndex.fixingDays(), overnightIndex,
                gearing, spread,
                refPeriodStart, refPeriodEnd,
                dayCounter, false)
      {
         // value dates
         Schedule sch = new MakeSchedule()
         .from(startDate)
         .to(endDate)
         .withTenor(new Period(1, TimeUnit.Days))
         .withCalendar(overnightIndex.fixingCalendar())
         .withConvention(overnightIndex.businessDayConvention())
         .backwards()
         .value();

         valueDates_ = sch.dates();
         Utils.QL_REQUIRE(valueDates_.Count >= 2, () => "degenerate schedule");

         // fixing dates
         n_ = valueDates_.Count - 1;
         if (overnightIndex.fixingDays() == 0)
         {
            fixingDates_ = new List<Date>(valueDates_);
         }
         else
         {
            fixingDates_ = new InitializedList<Date>(n_);
            for (int i = 0; i < n_; ++i)
               fixingDates_[i] = overnightIndex.fixingDate(valueDates_[i]);
         }

         // accrual (compounding) periods
         dt_ = new List<double>(n_);
         DayCounter dc = overnightIndex.dayCounter();
         for (int i = 0; i < n_; ++i)
            dt_.Add(dc.yearFraction(valueDates_[i], valueDates_[i + 1]));

         setPricer(new OvernightIndexedCouponPricer());

      }

      public List<double> indexFixings()
      {
         fixings_ = new InitializedList<double>(n_);
         for (int i = 0; i < n_; ++i)
            fixings_[i] = index_.fixing(fixingDates_[i]);
         return fixings_;
      }


      //! fixing dates for the rates to be compounded
      public List<Date> fixingDates() { return fixingDates_; }
      //! accrual (compounding) periods
      public List<double> dt() { return dt_; }
      //! value dates for the rates to be compounded
      public List<Date> valueDates() { return valueDates_; }

      private List<Date> valueDates_, fixingDates_;
      private List<double> fixings_;
      int n_;
      List<double> dt_;
   }

   //! helper class building a sequence of overnight coupons
   public class OvernightLeg : RateLegBase
   {
      public OvernightLeg(Schedule schedule, OvernightIndex overnightIndex)
      {
         schedule_ = schedule;
         overnightIndex_ = overnightIndex;
         paymentAdjustment_ = BusinessDayConvention.Following;
      }
      public new OvernightLeg withNotionals(double notional)
      {
         notionals_ = new List<double>(); notionals_.Add(notional);
         return this;
      }
      public new OvernightLeg withNotionals(List<double> notionals)
      {
         notionals_ = notionals;
         return this;
      }
      public OvernightLeg withPaymentDayCounter(DayCounter dayCounter)
      {
         paymentDayCounter_ = dayCounter;
         return this;
      }
      public new OvernightLeg withPaymentAdjustment(BusinessDayConvention convention)
      {
         paymentAdjustment_ = convention;
         return this;
      }
      public OvernightLeg withGearings(double gearing)
      {
         gearings_ = new List<double>(); gearings_.Add(gearing);
         return this;
      }
      public OvernightLeg withGearings(List<double> gearings)
      {
         gearings_ = gearings;
         return this;
      }
      public OvernightLeg withSpreads(double spread)
      {
         spreads_ = new List<double>(); spreads_.Add(spread);
         return this;
      }
      public OvernightLeg withSpreads(List<double> spreads)
      {
         spreads_ = spreads;
         return this;
      }

      public override List<CashFlow> value()
      {
         return CashFlowVectors.OvernightLeg(notionals_, schedule_, paymentAdjustment_, overnightIndex_, gearings_, spreads_, paymentDayCounter_);
      }

      private OvernightIndex overnightIndex_;
      private List<double> gearings_;
      private List<double> spreads_;
   }

}
