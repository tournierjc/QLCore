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
   public class ConstantCapFloorTermVolatility : CapFloorTermVolatilityStructure
   {
      private Handle<Quote> volatility_;

      //! floating reference date, floating market data
      public ConstantCapFloorTermVolatility(int settlementDays,
                                            Calendar cal,
                                            BusinessDayConvention bdc,
                                            Handle<Quote> volatility,
                                            DayCounter dc)
         : base(settlementDays, cal, bdc, dc)
      {
         volatility_ = volatility;
      }

      //! fixed reference date, floating market data
      public ConstantCapFloorTermVolatility(Date referenceDate,
                                            Calendar cal,
                                            BusinessDayConvention bdc,
                                            Handle<Quote> volatility,
                                            DayCounter dc)
         : base(referenceDate, cal, bdc, dc)
      {
         volatility_ = volatility;
      }

      //! floating reference date, fixed market data
      public ConstantCapFloorTermVolatility(int settlementDays,
                                            Calendar cal,
                                            BusinessDayConvention bdc,
                                            double volatility,
                                            DayCounter dc)
         : base(settlementDays, cal, bdc, dc)
      {
         volatility_ = new Handle<Quote>(new SimpleQuote(volatility));
      }

      // fixed reference date, fixed market data
      public ConstantCapFloorTermVolatility(Date referenceDate,
                                            Calendar cal,
                                            BusinessDayConvention bdc,
                                            double volatility,
                                            DayCounter dc)
         : base(referenceDate, cal, bdc, dc)
      {
         volatility_ = new Handle<Quote>(new SimpleQuote(volatility));
      }

      #region TermStructure interface

      public override Date maxDate()
      {
         return Date.maxDate();
      }

      #endregion


      #region VolatilityTermStructure interface
      public override double minStrike()
      {
         return Double.MinValue;
      }

      public override double maxStrike()
      {
         return Double.MaxValue;
      }

      #endregion


      protected override double volatilityImpl(double t, double rate)
      {
         return volatility_.link.value();
      }

   }
}
