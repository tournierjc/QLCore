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

namespace QLCore
{
   //! Term structure with added spread on the instantaneous forward rate
   /*! \note This term structure will remain linked to the original structure, i.e., any changes in the latter will be
             reflected in this structure as well.

       \ingroup yieldtermstructures

       \test
       - the correctness of the returned values is tested by checking them against numerical calculations.
       - observability against changes in the underlying term structure and in the added spread is checked.
   */
   public class ForwardSpreadedTermStructure : ForwardRateStructure
   {
      private Handle<YieldTermStructure> originalCurve_;
      private Handle<Quote> spread_;

      public ForwardSpreadedTermStructure(Handle<YieldTermStructure> h, Handle<Quote> spread)
      {
         originalCurve_ = h;
         spread_ = spread;
      }

      public override DayCounter dayCounter() { return originalCurve_.link.dayCounter(); }
      public override Calendar calendar() { return originalCurve_.link.calendar(); }
      public override int settlementDays() { return originalCurve_.link.settlementDays(); }
      public override Date referenceDate() { return originalCurve_.link.referenceDate(); }
      public override Date maxDate() { return originalCurve_.link.maxDate(); }
      public override double maxTime() { return originalCurve_.link.maxTime(); }

      //! returns the spreaded forward rate
      protected override double forwardImpl(double t)
      {
         return originalCurve_.link.forwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency, true).rate()
                + spread_.link.value();
      }

      //! returns the spreaded zero yield rate
      /* This method must disappear should the spread become a curve */
      protected override double zeroYieldImpl(double t)
      {
         return originalCurve_.link.zeroRate(t, Compounding.Continuous, Frequency.NoFrequency, true).rate()
                + spread_.link.value();
      }
   }
}
