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

using System.Collections.Generic;
using System.Linq;

namespace QLCore
{
   public class InterpolatedPiecewiseZeroSpreadedTermStructure<Interpolator> : ZeroYieldStructure
      where Interpolator : class, IInterpolationFactory, new ()
   {
      public InterpolatedPiecewiseZeroSpreadedTermStructure(Handle<YieldTermStructure> h,
                                                            List<Handle<Quote>> spreads,
                                                            List<Date> dates,
                                                            Compounding compounding = Compounding.Continuous,
                                                            Frequency frequency = Frequency.NoFrequency,
                                                            DayCounter dc = default(DayCounter),
                                                            Interpolator factory = default(Interpolator))
      {
         originalCurve_ = h;
         spreads_ = spreads;
         dates_ = dates;
         times_ = new InitializedList<double>(dates.Count);
         spreadValues_ = new InitializedList<double>(dates.Count);
         compounding_ = compounding;
         frequency_ = frequency;
         dc_ = dc ?? new DayCounter();
         factory_ = factory ?? FastActivator<Interpolator>.Create();

         Utils.QL_REQUIRE(!spreads_.empty(), () => "no spreads given");
         Utils.QL_REQUIRE(spreads_.Count == dates_.Count, () => "spread and date vector have different sizes");

         if (!originalCurve_.empty())
            updateInterpolation();
      }

      protected Handle<YieldTermStructure> originalCurve_;
      protected List<Handle<Quote>> spreads_;
      protected List<Date> dates_;
      protected List<double> times_;
      protected List<double> spreadValues_;
      protected Compounding compounding_;
      protected Frequency frequency_;
      protected DayCounter dc_;
      protected Interpolator factory_;
      protected Interpolation interpolator_;

      public override DayCounter dayCounter() { return originalCurve_.link.dayCounter(); }
      public override Calendar calendar() { return originalCurve_.link.calendar(); }
      public override int settlementDays() { return originalCurve_.link.settlementDays(); }
      public override Date referenceDate() { return originalCurve_.link.referenceDate(); }
      public override Date maxDate() { return originalCurve_.link.maxDate() < dates_.Last() ? originalCurve_.link.maxDate() : dates_.Last(); }

      protected override double zeroYieldImpl(double t)
      {
         double spread = calcSpread(t);
         InterestRate zeroRate = originalCurve_.link.zeroRate(t, compounding_, frequency_, true);
         InterestRate spreadedRate = new InterestRate(zeroRate.value() + spread,
                                                      zeroRate.dayCounter(),
                                                      zeroRate.compounding(),
                                                      zeroRate.frequency());
         return spreadedRate.equivalentRate(Compounding.Continuous, Frequency.NoFrequency, t).value();
      }

      protected double calcSpread(double t)
      {
         if (t <= times_.First())
            return spreads_.First().link.value();
         else if (t >= times_.Last())
            return spreads_.Last().link.value();
         else
            return interpolator_.value(t, true);
      }

      public override void update()
      {
         if (!originalCurve_.empty())
         {
            updateInterpolation();
            base.update();
         }
         else
         {
            /* The implementation inherited from YieldTermStructure
               asks for our reference date, which we don't have since
               the original curve is still not set. Therefore, we skip
               over that and just call the base-class behavior. */
            base.update();
         }
      }

      protected void updateInterpolation()
      {
         for (int i = 0; i < dates_.Count; i++)
         {
            times_[i] = timeFromReference(dates_[i]);
            spreadValues_[i] = spreads_[i].link.value();
         }
         interpolator_ = factory_.interpolate(times_, times_.Count, spreadValues_);
      }
   }

   public class PiecewiseZeroSpreadedTermStructure: InterpolatedPiecewiseZeroSpreadedTermStructure<Linear>
   {
      public PiecewiseZeroSpreadedTermStructure(Handle<YieldTermStructure> h,
                                                List<Handle<Quote>> spreads,
                                                List<Date> dates,
                                                Compounding compounding = Compounding.Continuous,
                                                Frequency frequency = Frequency.NoFrequency,
                                                DayCounter dc = default(DayCounter))
         : base(h, spreads, dates, compounding, frequency, dc, new Linear())
      { }
   }
}
