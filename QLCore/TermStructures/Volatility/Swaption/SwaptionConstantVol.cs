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
   //! Constant swaption volatility, no time-strike dependence
   public class ConstantSwaptionVolatility : SwaptionVolatilityStructure
   {
      private Handle<Quote> volatility_;
      private Period maxSwapTenor_;
      private VolatilityType volatilityType_;
      private double? shift_;

      //! floating reference date, floating market data
      public ConstantSwaptionVolatility(int settlementDays,
                                        Calendar cal,
                                        BusinessDayConvention bdc,
                                        Handle<Quote> vol,
                                        DayCounter dc,
                                        VolatilityType type = VolatilityType.ShiftedLognormal,
                                        double? shift = 0.0)
      : base(settlementDays, cal, bdc, dc)
      {
         volatility_ = vol;
         maxSwapTenor_ = new Period(100, TimeUnit.Years);
         volatilityType_ = type;
         shift_ = shift;
      }

      //! fixed reference date, floating market data
      public ConstantSwaptionVolatility(Date referenceDate,
                                        Calendar cal,
                                        BusinessDayConvention bdc,
                                        Handle<Quote> vol,
                                        DayCounter dc,
                                        VolatilityType type = VolatilityType.ShiftedLognormal,
                                        double? shift = 0.0)

      : base(referenceDate, cal, bdc, dc)
      {
         volatility_ = vol;
         maxSwapTenor_ = new Period(100, TimeUnit.Years);
         volatilityType_ = type;
         shift_ = shift;
      }

      //! floating reference date, fixed market data
      public ConstantSwaptionVolatility(int settlementDays,
                                        Calendar cal,
                                        BusinessDayConvention bdc,
                                        double vol,
                                        DayCounter dc,
                                        VolatilityType type = VolatilityType.ShiftedLognormal,
                                        double? shift = 0.0)
      : base(settlementDays, cal, bdc, dc)
      {
         volatility_ = new Handle<Quote>(new SimpleQuote(vol));
         maxSwapTenor_ = new Period(100, TimeUnit.Years);
         volatilityType_ = type;
         shift_ = shift;
      }

      //! fixed reference date, fixed market data
      public ConstantSwaptionVolatility(Date referenceDate,
                                        Calendar cal,
                                        BusinessDayConvention bdc,
                                        double vol,
                                        DayCounter dc,
                                        VolatilityType type = VolatilityType.ShiftedLognormal,
                                        double? shift = 0.0)
      : base(referenceDate, cal, bdc, dc)
      {
         volatility_ = new Handle<Quote>(new SimpleQuote(vol));
         maxSwapTenor_ = new Period(100, TimeUnit.Years);
         volatilityType_ = type;
         shift_ = shift;
      }

      // TermStructure interface
      public override Date maxDate()
      {
         return Date.maxDate();
      }
      // VolatilityTermStructure interface
      public override double minStrike()
      {
         return double.MinValue;
      }

      public override double maxStrike()
      {
         return double.MaxValue;
      }

      // SwaptionVolatilityStructure interface
      public override Period maxSwapTenor()
      {
         return maxSwapTenor_;
      }

      public override VolatilityType volatilityType()
      {
         return volatilityType_;
      }

      protected new SmileSection smileSectionImpl(Date d, Period p)
      {
         double atmVol = volatility_.link.value();
         return new FlatSmileSection(d, atmVol, dayCounter(), referenceDate());
      }

      protected override SmileSection smileSectionImpl(double optionTime, double time)
      {
         double atmVol = volatility_.link.value();
         return new FlatSmileSection(optionTime, atmVol, dayCounter());
      }

      protected new double volatilityImpl(Date date, Period period, double rate)
      {
         return volatility_.link.value();
      }

      protected override double volatilityImpl(double time, double t, double rate)
      {
         return volatility_.link.value();
      }

      protected override double shiftImpl(double optionTime, double swapLength)
      {
         base.shiftImpl(optionTime, swapLength);
         return Convert.ToDouble(shift_);
      }
   }
}
